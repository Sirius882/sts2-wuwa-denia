using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>橘子蛋糕 — Common Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaOrangeCake : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new BlockVar(9m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_orange_cake.png";

    public override bool GainsBlock => true;

    public DeniaOrangeCake()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "橘子蛋糕",
            Description: "获得{Block:diff()}点[gold]格挡[/gold]。抽2张牌。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await CardPileCmd.Draw(ctx, 2, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
