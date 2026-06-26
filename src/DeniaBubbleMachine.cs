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

/// <summary>泡泡机 — Uncommon Skill, 1e. 7 block + 7 if form switched this turn.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaBubbleMachine : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new BlockVar(7m, ValueProp.Move) };

    public override bool GainsBlock => true;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_bubble_machine.png";

    public DeniaBubbleMachine()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "泡泡机",
        Description: "获得{Block:diff()}点[gold]格挡[/gold]。\n若本回合切换过形态，再获得{Block:diff()}点[gold]格挡[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        if (DeniaFormHelper._formSwitchedThisTurn)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
