using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>忽略联动追加段数；直接在原伤害回调之前等待身体动画命中时点。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class DeniaSingleHitAttackPatch
{
    internal sealed class WaitState(AttackCommand command, int declaredHits, DeniaSingleHitVisual visual,
        Func<Task>? beforeDamage, Func<Task>? afterAnimation)
    {
        internal Func<Task>? BeforeDamage { get; } = beforeDamage;
        internal Func<Task>? AfterAnimation { get; } = afterAnimation;
        private DeniaSingleHitVisual? _visual = visual;
        private Task _firstImpact = visual.Impact;
        private Task? _pendingImpact;
        private readonly int _initialDamageRounds = command.Results.Count();
        private int _damageIndex;
        private int _clipIndex;

        internal Task WaitForImpact()
        {
            // 原版在一轮伤害及其钩子完成后才写入 Results。两种回调可以共用同一个命中等待。
            if (_pendingImpact != null && command.Results.Count() > _initialDamageRounds + _damageIndex)
                ConfirmDamage();
            if (_damageIndex >= declaredHits || !GodotObject.IsInstanceValid(_visual)) return Task.CompletedTask;
            if (_pendingImpact != null) return _pendingImpact;

            // 一段动作装不下本牌的全部段数时，为剩余段数播放下一段动作。
            if (_clipIndex >= _visual!.DamageImpactCount)
            {
                _visual = PlayRemaining(command, declaredHits - _damageIndex);
                if (_visual == null) return Task.CompletedTask;
                _firstImpact = _visual.Impact;
                _clipIndex = 0;
            }

            return _pendingImpact = _visual.ImpactAt(_clipIndex);
        }

        private void ConfirmDamage()
        {
            if (_pendingImpact == null) return;
            if (GodotObject.IsInstanceValid(_visual)) _visual!.ReleaseImpact(_pendingImpact);
            _pendingImpact = null;
            _damageIndex++;
            _clipIndex++;
        }

        internal void ReleaseRemainingImpacts()
        {
            if (GodotObject.IsInstanceValid(_visual)) _visual!.ReleaseRemainingImpacts(_firstImpact);
        }
    }

    private static DeniaSingleHitVisual? PlayRemaining(AttackCommand command, int hits) =>
        DeniaFormPatch.PlaySingleHit(command.Attacker!, multiHit: hits >= 2, fourHit: hits >= 4, tripleHit: hits == 3);

    internal static void Prefix(AttackCommand __instance, int ____hitCount,
        ref Func<Task>? ____beforeDamage, ref Func<Task>? ____afterAttackerAnim, out WaitState? __state)
    {
        __state = null;
        if (NonInteractiveMode.IsActive) return;
        var attacker = __instance.Attacker;
        if (!DeniaPinkAttackRule.TryStart(__instance.ModelSource as CardModel, attacker, ____hitCount, __instance.CardPlay)) return;
        var visual = PlayRemaining(__instance, ____hitCount);
        if (visual == null) return;

        var state = new WaitState(__instance, ____hitCount, visual, ____beforeDamage, ____afterAttackerAnim);
        __state = state;
        // 等到身体命中时点再生成目标命中特效；原伤害前回调仍按原顺序执行。
        ____afterAttackerAnim = async () =>
        {
            if (state.AfterAnimation != null) await state.AfterAnimation();
            await state.WaitForImpact();
        };
        ____beforeDamage = async () =>
        {
            if (state.BeforeDamage != null) await state.BeforeDamage();
            await state.WaitForImpact();
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
            state.ReleaseRemainingImpacts();
            command.BeforeDamage(state.BeforeDamage!);
            command.AfterAttackerAnim(state.AfterAnimation!);
        }
    }
}
