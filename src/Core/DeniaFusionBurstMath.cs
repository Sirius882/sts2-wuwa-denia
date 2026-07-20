#nullable enable
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Denia;

/// <summary>
/// 达妮娅聚爆数值：上限取「基础 5 + 额外聚爆上限 power」，比例向上取整。
/// 不走 GetFusionBurstCap（可能滞后于 cap power 实际层数）。
/// </summary>
public static class DeniaFusionBurstMath
{
    public const int BaseCap = 5;

    /// <summary>怪物当前聚爆上限 = 5 + AemeathFusionBurstCapPower 层数。</summary>
    public static int GetCanonicalCap(Creature? target)
    {
        if (target == null || target.IsDead)
            return BaseCap;
        int bonus = target.GetPower<AemeathFusionBurstCapPower>()?.Amount ?? 0;
        if (bonus < 0) bonus = 0;
        return BaseCap + bonus;
    }

    /// <summary>ceil(numerator/denominator * cap)，分母/分子须为正。</summary>
    public static int CeilRatioOfCap(Creature? target, int numerator, int denominator)
    {
        if (numerator <= 0 || denominator <= 0)
            return 0;
        int cap = GetCanonicalCap(target);
        // ceil(cap * num / den) = (cap * num + den - 1) / den
        return (cap * numerator + denominator - 1) / denominator;
    }

    /// <summary>ceil(cap * percent / 100)，例如 61%。</summary>
    public static int CeilPercentOfCap(Creature? target, int percent)
    {
        if (percent <= 0)
            return 0;
        int cap = GetCanonicalCap(target);
        return (cap * percent + 99) / 100;
    }
}
