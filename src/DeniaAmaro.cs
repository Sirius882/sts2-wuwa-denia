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

namespace Denia;

/// <summary>阿马罗 — Rare Power, 1e(upg:0e). 每次切换形态抽1张牌。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaAmaro : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_amaro.png";

    public DeniaAmaro()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "阿马罗",
        Description: "切换形态时，抽1张牌。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaAmaroPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeniaAmaroPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_amaro_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_amaro_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "阿马罗",
            Description: "每次切换形态时，抽牌。",
            SmartDescription: "每次切换形态时，抽{Amount}张牌。");
}
