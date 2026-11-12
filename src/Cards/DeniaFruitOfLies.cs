using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>谎言之实 — Uncommon Skill, 0e. Gain 1(2) DC; +1(2) DC if form switched this turn.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFruitOfLies : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fruit_of_lies.png";

    public DeniaFruitOfLies()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "谎言之实",
            Description: "获得{IfUpgraded:show:2|1}黯核。若本回合切换过形态，再获得{IfUpgraded:show:2|1}黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await DeniaResourceState.GainDarkCore(Owner.Creature, IsUpgraded ? 2 : 1, Owner.Creature, this);

        if (DeniaFormHelper.HasSwitchedFormThisTurn(Owner.Creature))
            await DeniaResourceState.GainDarkCore(Owner.Creature, IsUpgraded ? 2 : 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {

    }
}
