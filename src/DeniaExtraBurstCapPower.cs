using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>从远方: 所有附加聚爆上限额外+1层</summary>
public sealed class DeniaExtraBurstCapPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "从远方", Description: "所有附加聚爆上限效果额外附加1层。", SmartDescription: "所有附加聚爆上限效果额外附加1层。");
}
