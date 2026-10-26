using System.Collections.Generic;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>冻伤 — 持续减益状态，亚杜拉的月光剑的生命流失标记；其他伤害增加20%。</summary>
public sealed class DeniaFrostbitePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    // 复用游戏的 NOPE 兜底图标，确保战斗状态栏与悬浮提示都有图标。
    public override string? CustomPackedIconPath => "res://images/powers/missing_power.png";
    public override string? CustomBigIconPath => "res://images/powers/missing_power.png";

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        return target == Owner && !AemeathFusionBurstState.IsBurstProcessing ? 1.2m : 1m;
    }

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "冻伤", Description: "聚爆引爆和熔解会消除冻伤，其他伤害增加20%。", SmartDescription: "聚爆引爆和熔解会消除冻伤，其他伤害增加20%。");
}
