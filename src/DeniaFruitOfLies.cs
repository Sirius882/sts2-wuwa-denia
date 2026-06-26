using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>谎言之实 — Uncommon Skill, 2e. Gain 1 DC; +2 DC if form switched this turn.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFruitOfLies : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded ? Array.Empty<CardKeyword>() : new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fruit_of_lies.png";

    public DeniaFruitOfLies()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "谎言之实",
        Description: "获得1黯核。\n若本回合切换过形态，额外获得1黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);

        if (DeniaFormHelper._formSwitchedThisTurn)
            await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
