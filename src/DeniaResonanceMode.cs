using System;
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>共鸣模态·集谐 Power — 仅作标记，实际重定向在 Harmony 补丁中。</summary>
public sealed class DeniaResonanceModePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "共鸣模态·集谐",
            Description: "聚爆体系已转换为偏谐体系。",
            SmartDescription: "聚爆→偏谐，聚爆上限→减偏谐上限，无条件引爆→谐度破坏。");
}

/// <summary>辅助方法：检查生物是否处于共鸣模态。</summary>
public static class DeniaResonanceModeHelper
{
    public static bool IsActive(Creature? creature) =>
        creature?.GetPower<DeniaResonanceModePower>() != null;
}
