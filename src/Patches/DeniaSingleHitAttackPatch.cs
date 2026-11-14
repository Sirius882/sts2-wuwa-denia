using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>忽略联动追加段数；直接在原伤害回调之前等待身体动画中点。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class DeniaSingleHitAttackPatch
{
    internal sealed record WaitState(Func<Task>? Original, Func<Task>? AfterAnimation, DeniaPinkAttackVisual Visual, Task Impact);

    internal static void Prefix(AttackCommand __instance, int ____hitCount,
        ref Func<Task>? ____beforeDamage, ref Func<Task>? ____afterAttackerAnim, out WaitState? __state)
    {
        __state = null;
        if (NonInteractiveMode.IsActive || !DeniaPinkAttackRule.TryStart(
            __instance.ModelSource as CardModel, __instance.Attacker, ____hitCount)) return;
        var visual = DeniaFormPatch.PlayPinkSingleHit(__instance.Attacker!);
        if (visual == null) return;
        var previous = ____beforeDamage;
        var afterAnimation = ____afterAttackerAnim;
        var impact = visual.Impact;
        __state = new WaitState(previous, afterAnimation, visual, impact);
        // 等到身体中点再生成目标命中特效；原伤害前回调仍按原顺序执行。
        ____afterAttackerAnim = async () => {
            if (afterAnimation != null) await afterAnimation();
            await impact;
        };
        ____beforeDamage = async () => {
            try
            {
                if (previous != null) await previous();
                await impact;
            }
            finally { if (Godot.GodotObject.IsInstanceValid(visual)) visual.ReleaseImpact(impact); }
        };
    }

    internal static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result, WaitState? __state)
    {
        if (__state != null) __result = Restore(__result, __instance, __state);
    }

    private static async Task<AttackCommand> Restore(Task<AttackCommand> task, AttackCommand command, WaitState state)
    {
        try { return await task; }
        finally
        {
            if (Godot.GodotObject.IsInstanceValid(state.Visual)) state.Visual.ReleaseImpact(state.Impact);
            command.BeforeDamage(state.Original!);
            command.AfterAttackerAnim(state.AfterAnimation!);
        }
    }
}
