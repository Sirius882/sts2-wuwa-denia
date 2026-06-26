using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AemeathWw.Scripts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Denia;

/// <summary>
/// 聚爆引爆/熔解伤害会解除目标的冻伤效果。
/// 实现方式：在 ResolveMelt / TryTriggerAutoBurst 执行前移除目标身上的冻伤 Power。
/// </summary>
public static class DeniaFrostbiteRemovalPatch
{
    internal static void RemoveFrostbiteFrom(Creature target)
    {
        if (target == null || target.IsDead) return;
        var fb = target.GetPower<DeniaFrostbitePower>();
        if (fb != null)
            _ = PowerCmd.Remove(fb);
    }
}
// ---- Patch: ResolveMelt — 熔解伤害解除冻伤 ----
[HarmonyPatch]
public static class DeniaMeltRemoveFrostbitePatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(AemeathFusionBurstState),
            nameof(AemeathFusionBurstState.ResolveMelt));
    }

    public static void Prefix(Creature target)
    {
        DeniaFrostbiteRemovalPatch.RemoveFrostbiteFrom(target);
    }
}
// ---- Patch: TryTriggerAutoBurst — 聚爆引爆伤害解除冻伤 ----
[HarmonyPatch]
public static class DeniaAutoBurstRemoveFrostbitePatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(AemeathFusionBurstState),
            nameof(AemeathFusionBurstState.TryTriggerAutoBurst));
    }

    public static void Prefix(Creature target)
    {
        if (target?.CombatState == null) return;
        var enemies = target.CombatState.HittableEnemies
            .Where(e => !e.IsDead);
        foreach (var enemy in enemies)
            DeniaFrostbiteRemovalPatch.RemoveFrostbiteFrom(enemy);
    }
}
