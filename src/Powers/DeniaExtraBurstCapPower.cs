using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>从远方: 附加聚爆上限额外+ExtraAmount；已满40则改为熔解ExtraAmount次。</summary>
public class DeniaExtraBurstCapPower : CustomPowerModel
{
    public virtual int ExtraAmount => System.Math.Max(1, Amount);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "从远方",
        Description: "附加聚爆上限时额外附加层数。若目标聚爆上限已满40，改为熔解。不可叠加。",
        SmartDescription: "附加聚爆上限时额外附加{Amount}层。若目标聚爆上限已满40，改为熔解{Amount}次。不可叠加。");
}

public sealed class DeniaExtraBurstCapEnhancedPower : DeniaExtraBurstCapPower
{
    public override int ExtraAmount => 2;

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "从远方",
        Description: "附加聚爆上限时额外附加2层。若目标聚爆上限已满40，改为熔解2次。不可叠加。",
        SmartDescription: "附加聚爆上限时额外附加2层。若目标聚爆上限已满40，改为熔解2次。不可叠加。");
}
