#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>
/// 没入虚无 — Rare Power。获得蔽星；持有者的所有熔解都不消耗聚爆层数。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaImmerseIntoVoid : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_immerse_into_void.png";

    public DeniaImmerseIntoVoid()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "没入虚无",
        Description: "获得{IfUpgraded:show:4|2}[gold]蔽星[/gold]。你所有的[gold]熔解[/gold]都不消耗聚爆层数。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = play;
        int star = IsUpgraded ? 4 : 2;
        await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, star, Owner.Creature, this);
        await PowerCmd.Apply<DeniaImmerseIntoVoidPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

/// <summary>没入虚无：持有者的熔解不消耗聚爆层数。图标借用 Aemeath 聚爆轨迹。</summary>
public sealed class DeniaImmerseIntoVoidPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    // 借用 Aemeath「聚爆轨迹」power 图标。
    public override string? CustomPackedIconPath =>
        "res://aemeath-ww/ui/powers/aemeath_trajectory_fusion_burst_power.webp";
    public override string? CustomBigIconPath =>
        "res://aemeath-ww/ui/powers/aemeath_trajectory_fusion_burst_power.webp";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "没入虚无",
        Description: "你所有的熔解都不消耗聚爆层数。",
        SmartDescription: "你所有的熔解都不消耗聚爆层数。");
}
