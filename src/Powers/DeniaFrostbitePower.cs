using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>冻伤 — 持续减益状态。目前仅作为"亚杜拉的月光剑"的标记使用。</summary>
public sealed class DeniaFrostbitePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "冻伤", Description: "可被聚爆引爆伤害和熔解伤害消除。", SmartDescription: "可被聚爆引爆伤害和熔解伤害消除。");
}
