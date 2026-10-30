using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using TuneStrain;

namespace Denia;

public abstract class DeniaResourceRelic : CustomRelicModel, IOnAutoBurstRelic
{
    protected abstract int PinkVirtualMatter { get; }
    protected abstract int ResponseThreshold { get; }
    protected virtual bool GainDarkCoreInBlack => false;

    public static DeniaResourceRelic? Get(Creature creature)
    {
        if (creature.Player?.GetRelic<DeniaCounterfeitDwarfStar>() is { IsMelted: false } dwarf)
            return dwarf;
        return creature.Player?.GetRelic<DeniaTrickster>() is { IsMelted: false } trickster ? trickster : null;
    }

    public async Task OnFormChanged(bool toBlack, Creature applier, CardModel source, bool clearVM = true)
    {
        var creature = Owner.Creature;
        if (toBlack)
            await DeniaResourceState.GainVirtualMatter(creature, 10, applier, source);
        else if (clearVM)
        {
            // 切换前拥有7虚质 → 抽1张牌
            if (DeniaResourceState.GetVirtualMatter(creature) >= 7)
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, Owner);
            await DeniaResourceState.ClearVirtualMatter(creature, applier, source);
        }
    }

    /// <summary>粉态行动虚质：攻击 / 熔解 / 聚爆引爆各触发一次；数值由持有的遗物决定。</summary>
    public async Task GainFromPinkAction(bool fromAttackCard = false)
    {
        var creature = Owner.Creature;
        if (creature.IsDead || !DeniaFormHelper.IsPink(creature)) return;
        int vmAmount = PinkVirtualMatter;
        // 彩虹糖仅加成「打出攻击牌」路径（见 DeniaRainbowCandyJump 文案）。
        if (fromAttackCard)
        {
            var candyPower = creature.GetPower<DeniaRainbowCandyJumpPower>();
            if (candyPower != null && candyPower.Amount > 0)
                vmAmount += 2 * candyPower.Amount;
        }
        await DeniaResourceState.GainVirtualMatter(creature, vmAmount, creature, null!);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || Get(Owner.Creature) != this) return;
        bool attack = cardPlay.Card.Type == CardType.Attack;
        if (attack) await GainFromPinkAction(fromAttackCard: true);
        await TryTriggerRelicRandomResponse(Owner, ResponseThreshold, attack ? 2 : 1);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature)
            || Get(Owner.Creature) != this || Owner.Creature.IsDead) return;
        if (GainDarkCoreInBlack || DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, null!);
    }

    public Task OnAutoBurst(RelicModel ownerRelic, Creature target, Creature? applier, CardModel? source)
    {
        if ((applier is { IsPlayer: true } ? applier : source?.Owner?.Creature) != Owner.Creature || Get(Owner.Creature) != this)
            return Task.CompletedTask;
        return GainFromPinkAction();
    }

    private static async Task TryTriggerRelicRandomResponse(Player player, int threshold, int cardsPlayed)
    {
        var creature = player.Creature;

        await PowerCmd.Apply<DeniaRelicCardPlayedCounterPower>(
            new ThrowingPlayerChoiceContext(), creature, cardsPlayed, creature, null!);
        var counter = creature.GetPower<DeniaRelicCardPlayedCounterPower>();
        int now = counter != null ? (int)counter.Amount : 0;
        if (now < threshold) return;

        int remainder = now - threshold;
        if (remainder <= 0)
            await PowerCmd.Remove<DeniaRelicCardPlayedCounterPower>(creature);
        else if (counter != null)
            await PowerCmd.ModifyAmount(
                new ThrowingPlayerChoiceContext(), counter, -threshold, creature, null!);

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
