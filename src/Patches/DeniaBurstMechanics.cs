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

/// <summary>熔解结算：冻伤清除 + 遗物粉态虚质，使用 Aemeath 公开响应规则。</summary>
public static class DeniaMeltEffects
{
    private static readonly IAemeathMeltResponseRule Rule = new MeltResponse();
    public static void Init() => AemeathFusionBurstRules.RegisterMeltResponseRule(Rule);

    private sealed class MeltResponse : IAemeathMeltResponseRule
    {
        public int Priority => 1100;
        public bool ShouldApply(AemeathMeltContext context) => true;
        public async Task Apply(AemeathMeltContext context)
        {
            await DeniaFrostbiteRemovalPatch.RemoveFrostbiteFromAsync(context.Target);
            if (context.CounterOwner is { } owner && DeniaResourceRelic.Get(owner) is { } relic)
                await relic.GainFromPinkAction();
        }
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
        // 具体状态提供分母：额外附加 ceil(cap / denom)
        int extra = DeniaFusionBurstMath.CeilRatioOfCap(target, 1, pwr.RatioDenominator);
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
        int extra = DeniaFusionBurstMath.CeilRatioOfCap(target, 1, pwr.RatioDenominator);
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

        int extra = pwr.ExtraAmount;
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
