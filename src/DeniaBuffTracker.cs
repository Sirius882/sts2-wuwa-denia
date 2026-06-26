using System.Collections.Generic;
using System.Linq;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using HarmonyLib;

namespace Denia;

/// <summary>
/// 通用 Buff/Debuff 层数统计工具 + 本回合是否施加过增益/减益的追踪。
/// "借我用下" 和 "好累，让我歇会……" 共用。
/// </summary>
public static class DeniaBuffTracker
{
    /// <summary>本回合是否给玩家施加过增益或给敌人施加过减益。</summary>
    public static bool BuffOrDebuffAppliedThisTurn;

    private static readonly HashSet<Creature> _subscribed = new();

    public static void Init()
    {
        CombatManager.Instance.CombatSetUp += _ =>
        {
            BuffOrDebuffThisCombat();
            DeniaFormHelper._formSwitchedThisTurn = false;
        };
        CombatManager.Instance.TurnStarted += _ =>
        {
            BuffOrDebuffAppliedThisTurn = false;
            DeniaFormHelper._formSwitchedThisTurn = false;
        };
    }

    private static void BuffOrDebuffThisCombat()
    {
        _subscribed.Clear();
        BuffOrDebuffAppliedThisTurn = false;
    }

    /// <summary>订阅生物事件（首次遇到时订阅）。</summary>
    public static void EnsureSubscribed(Creature creature)
    {
        if (_subscribed.Add(creature))
        {
            creature.PowerApplied += OnPowerChanged;
            creature.PowerIncreased += OnPowerIncreased;
        }
    }

    private static void OnPowerChanged(PowerModel power)
    {
        if (power.Amount <= 0) return;
        if (power.Owner.IsPlayer && power.Type == PowerType.Buff)
            BuffOrDebuffAppliedThisTurn = true;
        else if (!power.Owner.IsPlayer && power.Type == PowerType.Debuff)
            BuffOrDebuffAppliedThisTurn = true;
    }

    private static void OnPowerIncreased(PowerModel power, int amount, bool _)
    {
        if (amount <= 0) return;
        if (power.Owner.IsPlayer && power.Type == PowerType.Buff)
            BuffOrDebuffAppliedThisTurn = true;
        else if (!power.Owner.IsPlayer && power.Type == PowerType.Debuff)
            BuffOrDebuffAppliedThisTurn = true;
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
// ---- Patch 15: Buff/Debuff 追踪 — 订阅新战斗中的所有生物 ----
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
public static class DeniaBuffTrackerSubscribePatch
{
    public static void Prefix(ICombatState combatState, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        foreach (var player in combatState.Players)
            DeniaBuffTracker.EnsureSubscribed(player.Creature);
        foreach (var enemy in combatState.Enemies)
            DeniaBuffTracker.EnsureSubscribed(enemy);
    }
}
