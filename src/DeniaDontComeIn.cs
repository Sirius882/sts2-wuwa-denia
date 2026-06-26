using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>不要···进来 — Common Skill, 1e(upg:0). Gain DC, switch black, extra VM, get cards.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaDontComeIn : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_dont_come_in.png";

    public DeniaDontComeIn()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "不要···进来",
        Description: "获得1黯核。若处于[gold]粉色[/gold]形态，切换到[gold]黑色[/gold]形态，额外获得6[gold]虚质[/gold]，获得「怜悯我」和「直视我」。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);

        if (DeniaFormHelper.IsPink(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
            // SwitchToBlack already gives 10 VM, add 6 more = 16 total
            await DeniaResourceState.GainVirtualMatter(Owner.Creature, 6, Owner.Creature, this);

            var cb = Owner.Creature.CombatState;
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaPityMe>(Owner), PileType.Hand, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaLookAtMe>(Owner), PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
