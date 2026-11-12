using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>从远方: 附加聚爆上限额外+ExtraAmount；已满40则改为熔解ExtraAmount次。</summary>
public class DeniaExtraBurstCapPower : CustomPowerModel
{
    public virtual int ExtraAmount => 1;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_extra_burst_cap_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "从远方",
        Description: $"附加聚爆上限效果额外附加{ExtraAmount}层。若目标聚爆上限已满40，改为熔解{ExtraAmount}次。不可叠加，可以覆盖方式升级。",
        SmartDescription: $"附加聚爆上限效果额外附加{ExtraAmount}层。若目标聚爆上限已满40，改为熔解{ExtraAmount}次。不可叠加，可以覆盖方式升级。");
}

public sealed class DeniaExtraBurstCapEnhancedPower : DeniaExtraBurstCapPower
{
    public override int ExtraAmount => 2;
}
