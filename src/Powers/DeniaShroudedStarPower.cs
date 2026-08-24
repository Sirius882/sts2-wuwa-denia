using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>蔽星 - 每层提高由持有者引发的 10% 聚爆引爆伤害和 20% 熔解伤害。</summary>
public sealed class DeniaShroudedStarPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_shrouded_star_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_shrouded_star_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "蔽星",
        Description: "每层使由你引发的聚爆引爆伤害提升10%，熔解伤害提升20%。",
        SmartDescription: "每层使由你引发的聚爆引爆伤害提升10%，熔解伤害提升20%。");
}
