using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Denia;

/// <summary>
/// 冻伤只在「熔解实际造成伤害」或「自动引爆成功」时清除。
/// 熔解清除已并入 DeniaResolveMeltEffectsPatch；本文件只保留 AutoBurst。
/// 仅叠聚爆层、未引爆时不清除。
/// </summary>
public static class DeniaFrostbiteRemovalPatch
{
    internal static async Task RemoveFrostbiteFromAsync(Creature target)
    {
        if (target == null || target.IsDead) return;
        var fb = target.GetPower<DeniaFrostbitePower>();
        if (fb != null)
            await PowerCmd.Remove(fb);
    }
}

[HarmonyPatch]
public static class DeniaAutoBurstRemoveFrostbitePatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(AemeathFusionBurstState), nameof(AemeathFusionBurstState.TryTriggerAutoBurst));

    public static void Postfix(ref Task<bool> __result, Creature target)
    {
        __result = Wrap(__result, target);
    }

    private static async Task<bool> Wrap(Task<bool> original, Creature target)
    {
        bool detonated = await (original ?? Task.FromResult(false));
        if (!detonated) return false;

        // 引爆对全体可命中敌人造成伤害 → 清除这些敌人身上的冻伤
        if (target?.CombatState != null)
        {
            foreach (var enemy in target.CombatState.HittableEnemies.Where(e => !e.IsDead).ToArray())
                await DeniaFrostbiteRemovalPatch.RemoveFrostbiteFromAsync(enemy);
        }
        else
        {
            await DeniaFrostbiteRemovalPatch.RemoveFrostbiteFromAsync(target);
        }

        return true;
    }
}
