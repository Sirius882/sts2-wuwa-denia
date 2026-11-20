using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>忽略联动追加段数；直接在原伤害回调之前等待身体动画命中时点。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class DeniaSingleHitAttackPatch
{
    internal sealed record WaitState(Func<Task>? Original, Func<Task>? AfterAnimation, DeniaSingleHitVisual Visual, Task Impact);
    private static readonly ConditionalWeakTable<AttackCommand, WaitState> Active = new();

    internal static void Prefix(AttackCommand __instance, int ____hitCount,
        ref Func<Task>? ____beforeDamage, ref Func<Task>? ____afterAttackerAnim, out WaitState? __state)
    {
        __state = null;
        if (NonInteractiveMode.IsActive) return;
        if (!DeniaPinkAttackRule.TryStart(__instance.ModelSource as CardModel, __instance.Attacker,
            ____hitCount)) return;
        var visual = DeniaFormPatch.PlaySingleHit(__instance.Attacker!, ____hitCount >= 2, ____hitCount >= 4, ____hitCount == 3);
        if (visual == null) return;
        // 粉态Fast仅播放动作，原攻击回调和伤害时序完全保留，不安装任何命中等待。
        if (!visual.WaitForDamage) return;
        var previous = ____beforeDamage;
        var afterAnimation = ____afterAttackerAnim;
        var impact = visual.Impact;
        var impacts = new Task[visual.DamageImpactCount];
        for (int i = 0; i < impacts.Length; i++) impacts[i] = visual.ImpactAt(i);
        int damageIndex = 0;
        __state = new WaitState(previous, afterAnimation, visual, impact);
        Active.Remove(__instance);
        Active.Add(__instance, __state);
        // 等到身体命中时点再生成目标命中特效；原伤害前回调仍按原顺序执行。
        ____afterAttackerAnim = async () => {
            if (afterAnimation != null) await afterAnimation();
            await impact;
        };
        ____beforeDamage = async () => {
            int index = damageIndex++;
            var hit = index < impacts.Length ? impacts[index] : null;
            try
            {
                if (previous != null) await previous();
                if (hit != null) await hit;
            }
            finally { if (hit != null && Godot.GodotObject.IsInstanceValid(visual)) visual.ReleaseImpact(hit); }
        };
    }

    internal static void ConfirmDamage(AttackCommand command)
    {
        if (Active.TryGetValue(command, out var state) && Godot.GodotObject.IsInstanceValid(state.Visual))
            state.Visual.ConfirmDamage(state.Impact);
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
            if (Active.TryGetValue(command, out var active) && ReferenceEquals(active, state)) Active.Remove(command);
            if (Godot.GodotObject.IsInstanceValid(state.Visual)) state.Visual.ReleaseRemainingImpacts(state.Impact);
            command.BeforeDamage(state.Original!);
            command.AfterAttackerAnim(state.AfterAnimation!);
        }
    }
}

/// <summary>Damage已结算并产生数字后，才开放普通模式下的动作替换；群攻每轮只确认一次。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.AddResultsInternal))]
internal static class DeniaAttackDamageConfirmedPatch
{
    internal static void Postfix(AttackCommand __instance) => DeniaSingleHitAttackPatch.ConfirmDamage(__instance);
}
