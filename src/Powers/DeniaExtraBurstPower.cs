using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 回到远方：所有附加聚爆时，额外附加目标当前上限 1/RatioDenominator 层（具体状态决定分母）。
/// 实际加成在 DeniaExtraBurstPatch 里按目标 cap 计算。
/// </summary>
public class DeniaExtraBurstPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // 比例分母（10 或 7）由具体状态决定，不叠加层数
    public virtual int RatioDenominator => 10;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "回到远方",
        Description: $"所有附加聚爆效果额外附加目标聚爆上限1/{RatioDenominator}的层数。不可叠加，可以覆盖方式升级。",
        SmartDescription: $"所有附加聚爆效果额外附加目标聚爆上限1/{RatioDenominator}的层数。不可叠加，可以覆盖方式升级。");
}

public sealed class DeniaExtraBurstEnhancedPower : DeniaExtraBurstPower
{
    public override int RatioDenominator => 7;
}
