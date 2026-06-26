using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>回到远方: 所有附加聚爆额外+1层</summary>
public sealed class DeniaExtraBurstPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "回到远方", Description: "所有附加聚爆效果额外附加1层。", SmartDescription: "所有附加聚爆效果额外附加1层。");
}
