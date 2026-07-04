using System.Threading.Tasks;
using AemeathWw.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 通用 Buff/Debuff 层数统计工具 + 本回合是否施加过增益/减益的追踪。
/// "借我用下" 和 "好累，让我歇会……" 共用。
/// </summary>
public static class DeniaBuffTracker
{
    public static void Init() { }

    public static async Task ClearTurnMarkers(ICombatState combatState)
    {
        foreach (var player in combatState.Players)
        {
            if (player.Character is not Denia) continue;
            await PowerCmd.Remove<DeniaBuffOrDebuffAppliedThisTurnPower>(player.Creature);
            await PowerCmd.Remove<DeniaFormSwitchedThisTurnPower>(player.Creature);
        }
    }

    public static bool WasBuffOrDebuffAppliedThisTurn(Creature creature) =>
        creature.GetPower<DeniaBuffOrDebuffAppliedThisTurnPower>()?.Amount > 0;

    internal static Creature? GetMarkerOwner(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0) return null;
        Creature target = power.Owner;

        if (target.IsPlayer && target.Player?.Character is Denia && power.Type == PowerType.Buff)
            return target;

        if (!target.IsPlayer && power.Type == PowerType.Debuff)
        {
            if (applier?.IsPlayer == true && applier.Player?.Character is Denia)
                return applier;
            if (cardSource?.Owner?.Character is Denia)
                return cardSource.Owner.Creature;
        }

        return null;
    }

    internal static async Task MarkBuffOrDebuffAppliedThisTurn(PlayerChoiceContext choiceContext, Creature creature)
    {
        if (creature.Player?.Character is not Denia) return;
        if (creature.GetPower<DeniaBuffOrDebuffAppliedThisTurnPower>() != null) return;
        await PowerCmd.Apply<DeniaBuffOrDebuffAppliedThisTurnPower>(choiceContext, creature, 1m, creature, null!);
    }

    /// <summary>统计玩家身上的增益总层数（PowerType.Buff, Amount > 0）。聚爆轨迹只计入十分之一。</summary>
    public static int CountPlayerBuffs(Creature player)
    {
        int count = 0;
        foreach (var p in player.Powers)
        {
            if (p.Type != PowerType.Buff || p.Amount <= 0) continue;
            if (p is AemeathFusionBurstTrajectoryPower)
                count += (int)p.Amount / 10;
            else
                count += (int)p.Amount;
        }
        return count;
    }

    /// <summary>统计敌人身上的减益总层数（PowerType.Debuff, Amount > 0）。</summary>
    public static int CountEnemyDebuffs(Creature enemy)
    {
        int count = 0;
        foreach (var p in enemy.Powers)
        {
            if (p.Type == PowerType.Debuff && p.Amount > 0)
                count += (int)p.Amount;
        }
        return count;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
public static class DeniaBuffTrackerPowerChangePatch
{
    public static void Postfix(
        ref Task __result,
        ICombatState combatState,
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        var markerOwner = DeniaBuffTracker.GetMarkerOwner(power, amount, applier, cardSource);
        if (markerOwner == null) return;
        __result = Wrap(__result, choiceContext, markerOwner);
    }

    private static async Task Wrap(Task original, PlayerChoiceContext choiceContext, Creature markerOwner)
    {
        await (original ?? Task.CompletedTask);
        await DeniaBuffTracker.MarkBuffOrDebuffAppliedThisTurn(choiceContext, markerOwner);
    }
}
