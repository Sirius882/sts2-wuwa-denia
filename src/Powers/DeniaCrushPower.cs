using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Denia;

/// <summary>
/// 轧碎：本场战斗中，你附加的聚爆引爆时不再群体等额结算，
/// 而是把本应造成的伤害总量（逐目标按其易伤/增伤预结算后求和）全部集中在被引爆的目标上。
/// 实际分配在 DeniaCrushDistributionPatch 注册的分配规则里接管。
/// </summary>
public sealed class DeniaCrushPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_crush_power.png";

    public override string? CustomBigIconPath => "res://images/ui/powers/denia_crush_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "轧碎",
            Description: "你的[color=#9A6A18]聚爆引爆[/color]伤害不再AOE结算，而是把本应造成的伤害总量全部集中在被引爆的单个目标上。敌方阵营只有1个目标时，按有2个目标计算。",
            SmartDescription: "聚爆引爆伤害总量全部集中在被引爆的单个目标上；敌方只有1个目标时按2个计算。");
}
