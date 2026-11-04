using System.Collections.Generic;
using BaseLib.Abstracts;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 回到远方：所有附加聚爆时，额外附加目标当前上限 1/RatioDenominator 层（具体状态决定分母）。
/// 实际加成在原生 power 加层钩子里按目标 cap 计算。
/// </summary>
public class DeniaExtraBurstPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // 比例分母（10 或 7）由具体状态决定，不叠加层数
    public virtual int RatioDenominator => 10;
    public virtual int FlatBonus => 0;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "回到远方",
        Description: $"所有附加聚爆效果额外附加{(FlatBonus > 0 ? $"{FlatBonus}+" : "")}目标聚爆上限1/{RatioDenominator}的层数。不可叠加，可以覆盖方式升级。",
        SmartDescription: $"所有附加聚爆效果额外附加{(FlatBonus > 0 ? $"{FlatBonus}+" : "")}目标聚爆上限1/{RatioDenominator}的层数。不可叠加，可以覆盖方式升级。");

    public override decimal ModifyPowerAmountGivenAdditive(
        PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource)
    {
        if (giver != Owner || target == null || amount <= 0 || DeniaBurstFillGuard.IsActive
            || power is not AemeathFusionBurstPower) return 0m;
        int extra = FlatBonus + DeniaFusionBurstMath.CeilRatioOfCap(target, 1, RatioDenominator);
        int remaining = AemeathFusionBurstState.GetFusionBurstCap(target) - AemeathFusionBurstState.GetFusionBurst(target);
        return System.Math.Max(0m, System.Math.Min(extra, remaining - amount));
    }

}

public class DeniaExtraBurstEnhancedPower : DeniaExtraBurstPower
{
    public override int RatioDenominator => 7;
}

public sealed class DeniaExtraBurstUpgradedPower : DeniaExtraBurstPower
{
    public override int FlatBonus => 2;
}

public sealed class DeniaExtraBurstUpgradedEnhancedPower : DeniaExtraBurstEnhancedPower
{
    public override int FlatBonus => 2;
}
