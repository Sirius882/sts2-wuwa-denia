#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐响应 power：附加在玩家身上的可见 buff，层数即"集谐响应标记总数"（由处决结算时覆盖写入）。
/// 层数通过 <see cref="TuneStrainMath.GetResponseDegree"/> 折算为集谐响应度 tsRes，
/// 参与集谐易伤倍率计算。持续回合数由多个独立的 <see cref="TuneStrainResponseDurationPower"/> 实例各自计量，
/// 本可见 power 始终显示并参与计算当前存续实例中的最高层数。
/// </summary>
public sealed class TuneStrainResponsePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_response.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_response.png";

    // 本地化只能内联提供：BaseLib 只把内联 Localization 写入当前语言表，
    // 且 TUNESTRAIN- 键不在 DeniaLocalizationOverridePatch 的 DENIA- 合并范围内。
    public override List<(string, string)>? Localization =>
        LocManager.Instance.Language switch
        {
            "zhs" => new PowerLoc(
                Title: "集谐响应",
                Description: "触发集谐处决时，统计你的牌组中带有[gold]集谐响应[/gold]标记的牌的总数。层数越高，从[gold]集谐·干涉[/gold]获得的增伤幅度越大。每次附加独立持续3回合，显示并按存续中的最高层数计算。",
                SmartDescription: "触发集谐处决时，统计你的牌组中带有集谐响应标记的牌的总数。层数越高，从集谐·干涉获得的增伤幅度越大。每次附加独立持续3回合，显示并按存续中的最高层数计算。"),
            _ => new PowerLoc(
                Title: "Tune Strain Response",
                Description: "When Tune Strain Resonance Break triggers, count the cards in your deck marked with [gold]Tune Strain Response[/gold]. Higher stacks increase the damage bonus from [gold]Tune Strain · Interference[/gold]. Each application lasts 3 turns independently; the highest active stack count is displayed and used.",
                SmartDescription: "When Tune Strain Resonance Break triggers, count the cards in your deck marked with Tune Strain Response. Higher stacks increase the damage bonus from Tune Strain · Interference. Each application lasts 3 turns independently; the highest active stack count is displayed and used.")
        };

}
