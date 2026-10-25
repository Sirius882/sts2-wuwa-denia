using System.Collections.Generic;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 蚀刻木桩：受到的聚爆引爆伤害与谐度破坏伤害翻倍（谐度破坏翻倍在
/// DeniaCrushDistributionPatch 注册的百分比修正器里实现）。
/// Amount = 木桩被玩家击杀时全队每人恢复的生命值 —— 这里仅作悬浮显示，
/// 实际的当场回血结算在玩家侧的 <see cref="DeniaEtchingStakeHealPower"/>
/// （木桩死亡会被移出战斗名单，其 power 收不到结算钩子，不能承担该结算）。
/// </summary>
public sealed class DeniaEtchingStakePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_etching_stake_power.png";

    public override string? CustomBigIconPath => "res://images/ui/powers/denia_etching_stake_power.png";

    // 死亡后保留：木桩死亡瞬间（伤害管线内）结算易伤翻倍不受影响；
    // 胜利回血已移交玩家侧的 DeniaEtchingStakeHealPower，本 power 不再参与死亡后结算。
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "蚀刻木桩",
            Description: "受到的[color=#9A6A18]聚爆引爆[/color]伤害和[color=#9A6A18]谐度破坏[/color]伤害翻倍。其被玩家击杀时，所有玩家立即恢复{Amount}点生命。",
            SmartDescription: "受到的聚爆引爆伤害和谐度破坏伤害翻倍。被玩家击杀时所有玩家立即恢复{Amount}点生命。");

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只翻倍聚爆引爆伤害（熔解不算）；轧碎集中结算落地的伤害已含本倍率，不再重复乘。
        if (target != Owner
            || !AemeathFusionBurstState.IsDetonationProcessing
            || AemeathFusionBurstState.IsConcentratedDetonationDelivery)
        {
            return 1m;
        }

        return 2m;
    }
}
