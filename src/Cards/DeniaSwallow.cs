#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;
using TuneStrain.Powers;

namespace Denia;

/// <summary>吞没 — Common Power: ±20% damage vs 集谐·干涉 (upg 30%).</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSwallow : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_swallow.png";

    public DeniaSwallow()
        : base(1, CardType.Power, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "吞没",
        Description: "带有[gold]集谐·干涉[/gold]的敌人对你造成的伤害-{IfUpgraded:show:30|20}%；你对带有[gold]集谐·干涉[/gold]的敌人造成的伤害+{IfUpgraded:show:30|20}%。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int percent = IsUpgraded ? 30 : 20;
        await PowerCmd.Apply<DeniaSwallowPower>(ctx, Owner.Creature, percent, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

/// <summary>
/// Amount = percent (20 or 30). Incoming DR and outgoing boost vs 集谐·干涉.
/// 覆盖：powered 攻击 + 熔解/聚爆引爆（Aemeath 用 Unpowered，靠 IsBurstProcessing 识别）。
/// </summary>
public sealed class DeniaSwallowPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_swallow_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_swallow_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "吞没",
        Description: "带有集谐·干涉的敌人对你造成的伤害-{Amount}%；你对带有集谐·干涉的敌人造成的伤害+{Amount}%。",
        SmartDescription: "带有集谐·干涉的敌人对你造成的伤害-{Amount}%；你对带有集谐·干涉的敌人造成的伤害+{Amount}%。");

    /// <summary>
    /// 熔解 / 自动引爆伤害走 ValueProp.Unpowered，不吃力量/易伤；
    /// 但 Aemeath 在结算时会把 IsBurstProcessing 置 true，用它识别这两类伤害。
    /// </summary>
    private static bool IsSwallowEligibleDamage(ValueProp props)
    {
        if (props.IsPoweredAttack())
            return true;

        // 熔解 / 聚爆引爆：Unpowered + 正在 burst 处理中
        return AemeathFusionBurstState.IsBurstProcessing
            && props.HasFlag(ValueProp.Unpowered);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!IsSwallowEligibleDamage(props))
            return 1m;

        int pct = Amount;
        if (pct <= 0) return 1m;

        // 带有集谐·干涉的敌人对你造成的伤害降低
        if (target == Owner && dealer != null && dealer.GetPower<TuneStrainInterferencePower>() != null)
            return 1m - pct / 100m;

        // 你对带有集谐·干涉的敌人造成的伤害提高（含攻击 / 熔解 / 引爆）
        if (dealer == Owner && target != null && target.GetPower<TuneStrainInterferencePower>() != null)
            return 1m + pct / 100m;

        return 1m;
    }
}
