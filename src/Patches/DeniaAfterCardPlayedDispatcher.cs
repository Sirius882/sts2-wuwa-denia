using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Denia;

/// <summary>
/// AfterCardPlayed 唯一 ref Task 包装入口。
/// 顺序固定：原钩子（含遗物的粉态虚质/响应） → 大师之剑 → 继续逃啊/你也试试。
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
        DeniaCardVoice.TryPlay(cardPlay.Card);   // 卡牌语音/音效（互不干扰游戏逻辑）
        __result = Dispatch(__result, player, cardPlay);
    }

    private static async Task Dispatch(Task original, Player player, CardPlay cardPlay)
    {
        try
        {
            await (original ?? Task.CompletedTask);

            await ProcessMasterSwordCounter(player, cardPlay);
            await DeniaKeepRunningPower.OnAnyCardPlayed(player, cardPlay);
            await DeniaYouTryItPower.OnAnyCardPlayed(player, cardPlay);
        }
        finally
        {
            // clear attack-card flag for black FX if/elif on next card
            try
            {
                if (player?.Creature != null)
                    DeniaFormPatch.EndBlackFormCardPlay(player.Creature);
            }
            catch { }
        }
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

}
