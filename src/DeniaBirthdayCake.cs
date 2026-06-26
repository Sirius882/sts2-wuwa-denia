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

/// <summary>生日蛋糕 — Rare Power, 3e(upg:2e Innate). +1 DC per turn in pink.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaBirthdayCake : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded ? new[] { CardKeyword.Innate } : Array.Empty<CardKeyword>();

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_birthday_cake.png";

    public DeniaBirthdayCake()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "生日蛋糕",
        Description: "[gold]粉色[/gold]形态下，每回合开始时额外获得1黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaBirthdayCakePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Innate);
    }
}
public sealed class DeniaBirthdayCakePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_birthday_cake_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_birthday_cake_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "生日蛋糕",
            Description: "粉色形态下每回合开始时获得的黯核+1。",
            SmartDescription: "粉色形态下，每回合开始时黯核额外+{Amount}。");
}
