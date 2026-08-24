using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 回到远方：所有附加聚爆时，额外附加目标当前上限 1/Amount 层（Amount 为分母）。
/// 实际加成在 DeniaExtraBurstPatch 里按目标 cap 计算。
/// </summary>
public sealed class DeniaExtraBurstPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // Amount = 比例分母（10 或 7），不叠加层数
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "回到远方",
        Description: "所有附加聚爆效果额外附加目标聚爆上限1/{Amount}的层数。",
        SmartDescription: "所有附加聚爆效果额外附加目标聚爆上限1/{Amount}的层数。");
}
