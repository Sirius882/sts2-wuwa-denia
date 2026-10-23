#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐响应 power：附加在玩家身上的可见 buff，层数即"集谐响应标记总数"（由处决结算时覆盖写入）。
/// 层数通过 <see cref="TuneStrainMath.GetResponseDegree"/> 折算为集谐响应度 tsRes，
/// 参与集谐易伤倍率计算。持续回合数由隐藏的 <see cref="TuneStrainResponseDurationPower"/> 计量。
/// 重复处决时保留原层数与本次响应标记总数的较大值，持续时间重置为 3 回合。
/// </summary>
public sealed class TuneStrainResponsePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_response.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_response.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "集谐响应",
            Description: "触发集谐处决时，统计你的抽牌堆、弃牌堆和手牌中带有[gold]集谐响应[/gold]标记的牌的总数，层数取该总数与原层数的较高值。层数越高，从[gold]集谐·干涉[/gold]获得的增伤幅度越大。持续3回合；再次触发集谐处决时，持续时间重置为3回合。",
            SmartDescription: "触发集谐处决时，统计你的抽牌堆、弃牌堆和手牌中带有集谐响应标记的牌的总数，层数取该总数与原层数的较高值。层数越高，从集谐·干涉获得的增伤幅度越大。持续3回合；再次触发集谐处决时，持续时间重置为3回合。");

}
