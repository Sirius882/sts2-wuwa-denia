using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>引爆事件——骗术师/赝作矮星等订阅</summary>
public static class DeniaBurstEvents
{
    public static event Func<Creature, Creature, int, Task>? OnBurstDone;
    private static bool _isFiring;

    internal static async Task FireBurst(Creature target, Creature applier, int cap)
    {
        if (_isFiring) return;
        _isFiring = true;
        try
        {
            if (OnBurstDone != null)
            {
                try { await OnBurstDone.Invoke(target, applier, cap); }
                catch (Exception ex) { GD.PrintErr($"[Denia] FireBurst handler error: {ex.Message}"); }
            }
        }
        finally { _isFiring = false; }
    }
}

/// <summary>
/// 粉态行动虚质：攻击 / 熔解 / 聚爆引爆各触发一次 +2。
/// 彩虹糖仅加成「打出攻击牌」路径（见 DeniaRainbowCandyJump 文案）。
/// </summary>
public static class DeniaPinkVirtualMatter
{
    public static async Task TryGainFromPinkAction(Creature? creature, bool fromAttackCard = false)
    {
        if (creature == null || creature.IsDead) return;
        if (!creature.IsPlayer) return;
        if (!DeniaFormHelper.IsPink(creature)) return;

        int vmAmount = 2;
        if (fromAttackCard)
        {
            var candyPower = creature.GetPower<DeniaRainbowCandyJumpPower>();
            if (candyPower != null && candyPower.Amount > 0)
                vmAmount += 2 * (int)candyPower.Amount;
        }
        await DeniaResourceState.GainVirtualMatter(creature, vmAmount, creature, null!);
    }
}

/// <summary>
/// 引爆检测：await 原 Task 保留返回值；层数较调用前下降视为引爆（比 after&lt;amount 更准）。
/// </summary>
[HarmonyPatch]
public static class DeniaBurstHook
{
    private static bool _burstInProgress;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathFusionBurstState), "TryAddFusionBurst",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(CardModel) });
    }

    public static void Postfix(ref Task<bool> __result, Creature target, int amount, Creature applier)
    {
        if (amount <= 0) return;
        if (_burstInProgress) return;
        __result = WrapBurstDetection(__result, target, amount, applier);
    }

    private static async Task<bool> WrapBurstDetection(
        Task<bool> originalTask, Creature target, int amount, Creature applier)
    {
        int before;
        try { before = AemeathFusionBurstState.GetFusionBurst(target); }
        catch { before = 0; }

        bool originalResult;
        try { originalResult = await originalTask; }
        catch { throw; }

        int after;
        try { after = AemeathFusionBurstState.GetFusionBurst(target); }
        catch { return originalResult; }

        // 自动引爆会清层/降层；仅叠层时 after >= before
        if (after >= before) return originalResult;

        _burstInProgress = true;
        try
        {
            var cap = AemeathFusionBurstState.GetFusionBurstCap(target);
            await DeniaBurstEvents.FireBurst(target, applier, cap);
        }
        finally { _burstInProgress = false; }

        return originalResult;
    }
}

/// <summary>
/// ResolveMelt 唯一 ref Task 包装：冻伤清除 + 粉态熔解虚质。
/// 禁止再对 ResolveMelt 增加第二个 ref Task 包装。
/// </summary>
[HarmonyPatch]
public static class DeniaResolveMeltEffectsPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(AemeathFusionBurstState), nameof(AemeathFusionBurstState.ResolveMelt));

    public static void Postfix(ref Task<int> __result, Creature target, Creature? applier)
    {
        __result = Wrap(__result, target, applier);
    }

    private static async Task<int> Wrap(Task<int> original, Creature target, Creature? applier)
    {
        int damage = await (original ?? Task.FromResult(0));
        if (damage <= 0) return damage;

        await DeniaFrostbiteRemovalPatch.RemoveFrostbiteFromAsync(target);
        await DeniaPinkVirtualMatter.TryGainFromPinkAction(applier);
        return damage;
    }
}

public static class DeniaRelicBurstHandler
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        DeniaBurstEvents.OnBurstDone += OnBurst;
    }

    private static async Task OnBurst(Creature target, Creature applier, int cap)
    {
        if (target.IsDead) return;
        if (applier?.IsPlayer != true) return;
        var player = applier.Player;
        if (player == null) return;

        bool hasTeddy = player.GetRelic<DeniaTrickster>() != null;
        bool hasDwarf = player.GetRelic<DeniaCounterfeitDwarfStar>() != null;
        if (hasTeddy || hasDwarf)
        {
            // 赝作的矮星：上限 1/3 向上取整；骗术师：上限 1/4 向下取整。
            int divisor = hasDwarf ? 3 : 4;
            int add = hasDwarf
                ? (cap + divisor - 1) / divisor
                : cap / divisor;
            if (add > 0)
                await AemeathFusionBurstState.TryAddFusionBurstWithoutAutoBurst(
                    target, add, applier, null!);
        }

        await DeniaPinkVirtualMatter.TryGainFromPinkAction(applier);
    }
}

/// <summary>熔解不耗层：按卡实例 + 没入虚无 + 相册粉态。</summary>
public static class DeniaMeltProtectPatch
{
    private static readonly IAemeathMeltConsumeRule Rule = new DeniaMeltPreserveRule();
    private static bool _registered;
    private static readonly HashSet<CardModel> PreservedCards = new();
    private static readonly object LockObj = new();

    public static void Init()
    {
        if (_registered) return;
        _registered = true;
        AemeathFusionBurstRules.RegisterMeltConsumeRule(Rule);
    }

    public static IDisposable BeginPreserve(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        lock (LockObj) PreservedCards.Add(card);
        return new PreserveScope(card);
    }

    private sealed class PreserveScope : IDisposable
    {
        private readonly CardModel _card;
        private bool _disposed;

        public PreserveScope(CardModel card) { _card = card; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (LockObj) PreservedCards.Remove(_card);
        }
    }

    private sealed class DeniaMeltPreserveRule : IAemeathMeltConsumeRule
    {
        public int Priority => 200;

        public int GetConsumedFusionBurst(AemeathMeltContext context, int currentConsumed)
        {
            if (currentConsumed <= 0) return currentConsumed;

            var source = context.Source;
            if (source != null)
            {
                bool preserve;
                lock (LockObj) preserve = PreservedCards.Contains(source);
                if (preserve) return 0;
            }

            Creature? applier = context.Applier ?? context.Source?.Owner?.Creature;
            if (applier?.IsPlayer == true && applier.GetPower<DeniaImmerseIntoVoidPower>() != null)
                return 0;

            if (applier?.IsPlayer != true) return currentConsumed;
            if (applier.Player?.GetRelic<DeniaAlbum>() == null) return currentConsumed;
            if (!DeniaFormHelper.IsPink(applier)) return currentConsumed;
            return 0;
        }
    }
}

/// <summary>蔽星：引爆 +10%/层，熔解 +20%/层。</summary>
public static class DeniaShroudedStarDamagePatch
{
    private static readonly IAemeathAutoBurstDamageRule AutoBurstRule = new ShroudedStarAutoBurstDamageRule();
    private static readonly IAemeathMeltDamageRule MeltRule = new ShroudedStarMeltDamageRule();
    private static bool _registered;

    public static void Init()
    {
        if (_registered) return;
        _registered = true;
        AemeathFusionBurstRules.RegisterAutoBurstDamageRule(AutoBurstRule);
        AemeathFusionBurstRules.RegisterMeltDamageRule(MeltRule);
    }

    private sealed class ShroudedStarAutoBurstDamageRule : IAemeathAutoBurstDamageRule
    {
        public int Priority => 100;

        public int Apply(AemeathAutoBurstContext context, int currentDamage)
        {
            if (currentDamage <= 0) return currentDamage;
            var applier = context.Applier;
            if (applier?.IsPlayer != true) return currentDamage;
            var power = applier.GetPower<DeniaShroudedStarPower>();
            if (power == null || power.Amount <= 0) return currentDamage;
            return currentDamage + currentDamage * (int)power.Amount * 10 / 100;
        }
    }

    private sealed class ShroudedStarMeltDamageRule : IAemeathMeltDamageRule
    {
        public int Priority => 100;

        public int Apply(AemeathMeltContext context, int currentDamage)
        {
            if (currentDamage <= 0) return currentDamage;
            var applier = context.Applier;
            if (applier?.IsPlayer != true) return currentDamage;
            var power = applier.GetPower<DeniaShroudedStarPower>();
            if (power == null || power.Amount <= 0) return currentDamage;
            return currentDamage + currentDamage * (int)power.Amount * 20 / 100;
        }
    }
}

[HarmonyPatch]
public static class DeniaExtraBurstPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathFusionBurstState), "TryAddFusionBurst",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(CardModel) });
    }

    public static void Prefix(Creature target, ref int amount, Creature applier)
    {
        if (DeniaBurstFillGuard.IsActive) return;
        if (applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaExtraBurstPower>();
        if (pwr == null || pwr.Amount <= 0) return;
        // Amount = 分母：额外附加 ceil(cap / denom)
        int extra = DeniaFusionBurstMath.CeilRatioOfCap(target, 1, (int)pwr.Amount);
        if (extra > 0) amount += extra;
    }
}

[HarmonyPatch]
public static class DeniaExtraBurstWithoutAutoBurstPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathFusionBurstState), "TryAddFusionBurstWithoutAutoBurst",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(CardModel) });
    }

    public static void Prefix(Creature target, ref int amount, Creature applier)
    {
        if (DeniaBurstFillGuard.IsActive) return;
        if (applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaExtraBurstPower>();
        if (pwr == null || pwr.Amount <= 0) return;
        int extra = DeniaFusionBurstMath.CeilRatioOfCap(target, 1, (int)pwr.Amount);
        if (extra > 0) amount += extra;
    }
}

[HarmonyPatch]
public static class DeniaExtraBurstCapPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathFusionBurstState), "TryIncreaseFusionBurstCap",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(CardModel) });
    }

    public static void Prefix(Creature target, ref int amount, Creature applier, out int __state)
    {
        __state = 0;
        if (DeniaBurstFillGuard.IsActive) return;
        if (applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaExtraBurstCapPower>();
        if (pwr == null || pwr.Amount <= 0) return;

        int extra = (int)pwr.Amount;
        if (AemeathFusionBurstState.GetFusionBurstCap(target) >= 40)
        {
            __state = extra;
            amount = 0;
            return;
        }

        amount += extra;
    }

    public static void Postfix(Creature target, Creature applier, CardModel source, int __state, ref Task<bool> __result)
    {
        if (__state <= 0) return;
        __result = WrapMelt(__result, target, applier, source, __state);
    }

    private static async Task<bool> WrapMelt(Task<bool> original, Creature target, Creature applier, CardModel source, int meltTimes)
    {
        bool ok = false;
        if (original != null) ok = await original;
        if (target == null || target.IsDead || meltTimes <= 0) return ok;
        await AemeathFusionBurstState.ResolveMelt(target, applier, source, meltTimes);
        return true;
    }
}
