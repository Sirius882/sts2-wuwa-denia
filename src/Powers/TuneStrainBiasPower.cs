#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐·偏移：附加在怪物身上的可见 debuff。
/// 偏移被集谐处决时清空并转化为等量的集谐·干涉；至少3层时，本次谐度破坏自身也获得集谐增伤。
/// 层数即 tsBias（处决前的偏移层数），用于决定后续集谐易伤的底数 tsBias2Base。
/// 旧规则记录（2026-10-23 已废止）：
/// 拥有集谐·干涉的目标不能被附加集谐·偏移（在 <see cref="TuneStrainState.TryAddBias"/> 中检查）。
/// 当前规则：无论目标是否拥有集谐·干涉，都允许附加集谐·偏移。
/// </summary>
public sealed class TuneStrainBiasPower : CustomPowerModel, IModifyResonanceBreakDamagePercentPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_bias.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_bias.png";

    public decimal ModifyResonanceBreakDamagePercent(
        Creature target, Creature applier, CardModel source, bool isUnconditional, decimal currentPercent)
    {
        if (target != Owner || Amount < 3)
            return currentPercent;

        // 此时偏移尚未转换；只修正百分比，保留原伤害范围排除以防已有干涉重复增伤。
        double ratio = TuneStrainMath.DamageAdvantageRatio(Amount, TuneStrainState.GetResponseDegree(applier));
        return currentPercent * (decimal)(1.0 + ratio);
    }

    public override List<(string, string)>? Localization =>
        LocManager.Instance.Language switch
        {
            "zhs" => new PowerLoc(
                Title: "集谐·偏移",
                Description: "触发谐度破坏后转化为[gold]集谐·干涉[/gold]。至少3层时，本次谐度破坏自身也获得集谐增伤。",
                SmartDescription: "触发谐度破坏后转化为集谐·干涉。至少3层时，本次谐度破坏自身也获得集谐增伤。"),
            _ => new PowerLoc(
                Title: "Tune Strain · Bias",
                Description: "When Resonance Break triggers, this is converted into [gold]Tune Strain · Interference[/gold]. At 3 or more stacks, this Resonance Break's own damage also gains the Tune Strain damage bonus.",
                SmartDescription: "When Resonance Break triggers, this is converted into Tune Strain · Interference. At 3 or more stacks, this Resonance Break's own damage also gains the Tune Strain damage bonus.")
        };
}
