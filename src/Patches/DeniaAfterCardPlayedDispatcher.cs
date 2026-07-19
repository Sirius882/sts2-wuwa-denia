using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using TuneStrain;

namespace Denia;

/// <summary>
/// AfterCardPlayed 唯一 ref Task 包装入口。
/// 顺序固定：原钩子 → 粉态虚质 → 大师之剑 → 继续逃啊/你也试试 → 骗术师响应。
/// 熵变/松子已在 AfterPowerAmountChanged 实时结算，不再此处 flush。
/// 禁止再对 AfterCardPlayed 增加第二个 ref Task __result 包装（见 AGENTS #75）。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class DeniaAfterCardPlayedDispatcher
{
    public static void Postfix(ref Task __result, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        _ = choiceContext;
        var player = cardPlay.Card.Owner;
        if (player == null) return;
        __result = Dispatch(__result, player, cardPlay);
    }

    private static async Task Dispatch(Task original, Player player, CardPlay cardPlay)
    {
        await (original ?? Task.CompletedTask);

        await TryGainPinkAttackVirtualMatter(player, cardPlay);
        await ProcessMasterSwordCounter(player, cardPlay);
        await DeniaKeepRunningPower.OnAnyCardPlayed(player, cardPlay);
        await DeniaYouTryItPower.OnAnyCardPlayed(player, cardPlay);
        await TryTriggerRelicRandomResponseIfNeeded(player);
    }

    private static async Task TryGainPinkAttackVirtualMatter(Player player, CardPlay cardPlay)
    {
        if (player.Character is not Denia) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        await DeniaPinkVirtualMatter.TryGainFromPinkAction(player.Creature, fromAttackCard: true);
    }

    private static async Task ProcessMasterSwordCounter(Player player, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack) return;
        var sword = player.GetRelic<DeniaMasterSword>();
        if (sword == null) return;

        bool isBoss = player.RunState.CurrentRoom.RoomType == RoomType.Boss;
        if (isBoss) return;
        if (sword.Counter <= 0) return;

        sword.Counter--;
        sword.RefreshDisplay();
        if (sword.Counter != 0 || (sword.GrantedStrength <= 0 && sword.GrantedShroudedStar <= 0))
            return;

        var str = player.Creature.GetPower<StrengthPower>();
        if (str != null && str.Amount >= sword.GrantedStrength && sword.GrantedStrength > 0)
            await PowerCmd.ModifyAmount(
                new ThrowingPlayerChoiceContext(), str, -sword.GrantedStrength, player.Creature, null!);
        if (sword.GrantedShroudedStar > 0)
        {
            var star = player.Creature.GetPower<DeniaShroudedStarPower>();
            if (star != null && star.Amount >= sword.GrantedShroudedStar)
                await PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(), star, -sword.GrantedShroudedStar, player.Creature, null!);
        }
        sword.GrantedStrength = 0;
        sword.GrantedShroudedStar = 0;
    }

    private static async Task TryTriggerRelicRandomResponseIfNeeded(Player player)
    {
        bool hasTeddy = player.GetRelic<DeniaTrickster>() != null;
        bool hasDwarf = player.GetRelic<DeniaCounterfeitDwarfStar>() != null;
        if (!hasTeddy && !hasDwarf) return;
        await TryTriggerRelicRandomResponse(player, hasDwarf);
    }

    private static async Task TryTriggerRelicRandomResponse(Player player, bool hasDwarf)
    {
        var creature = player.Creature;
        int threshold = hasDwarf ? 2 : 3;

        await PowerCmd.Apply<DeniaRelicCardPlayedCounterPower>(
            new ThrowingPlayerChoiceContext(), creature, 1m, creature, null!);
        var counter = creature.GetPower<DeniaRelicCardPlayedCounterPower>();
        int now = counter != null ? (int)counter.Amount : 0;
        if (now < threshold) return;

        await PowerCmd.Remove<DeniaRelicCardPlayedCounterPower>(creature);

        var eligible = new List<CardModel>();
        PileType[] piles = { PileType.Draw, PileType.Hand, PileType.Discard };
        foreach (var pt in piles)
        {
            IReadOnlyList<CardModel> cards;
            try { cards = pt.GetPile(player).Cards; }
            catch (System.InvalidOperationException) { continue; }
            foreach (var c in cards)
            {
                if (c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)) continue;
                if (TuneStrainState.HasTemporaryResponse(c)) continue;
                eligible.Add(c);
            }
        }

        if (eligible.Count == 0) return;
        if (player.RunState is MegaCrit.Sts2.Core.Runs.NullRunState) return;

        var chosen = player.RunState.Rng.CombatCardSelection.NextItem(eligible);
        if (chosen == null) return;
        TuneStrainState.AddTemporaryResponse(player, chosen);
    }
}
