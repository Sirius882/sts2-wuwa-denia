#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>卡纽 — Rare Power, 3e. 每次切换形态获得1能量。升级: +2力量+20聚爆轨迹。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaKaNiu : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_ka_niu.png";

    public DeniaKaNiu()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "卡纽",
        Description: "每次切换形态时，获得1点能量。{IfUpgraded:show:获得2点[gold]力量[/gold]和20层[gold]聚爆轨迹[/gold]。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaKaNiuPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        if (IsUpgraded)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, 2m, Owner.Creature, this);
            await PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(ctx, Owner.Creature, 20m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaKaNiuPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_ka_niu_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_ka_niu_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "卡纽",
            Description: "每次切换形态时，获得能量。",
            SmartDescription: "每次切换形态时，获得{Amount}点能量。");
}
