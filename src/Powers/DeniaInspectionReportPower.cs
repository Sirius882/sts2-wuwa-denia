using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Denia;

public sealed class DeniaInspectionReportPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_crush_power.png";
    public override string? CustomBigIconPath => CustomPackedIconPath;
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "检验报告RA2499-G",
        Description: "聚爆引爆的群体伤害总量集中在被引爆的单个目标上。",
        SmartDescription: "聚爆引爆的群体伤害总量集中在被引爆的单个目标上。");
}
