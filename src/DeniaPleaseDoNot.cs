using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

[BaseLib.Utils.Pool(typeof(DeniaCardPool))]
public sealed class DeniaPleaseDoNot : CustomCardModel, ITranscendenceCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_please_do_not.png";

    public DeniaPleaseDoNot()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, showInCardLibrary: false) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "请您不要···",
        Description: "只在[gold]粉色[/gold]形态下有效。\n切换到[gold]黑色[/gold]形态，获得「直视我」和「怜悯我」。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsPink(Owner.Creature))
            return;

        await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);

        var cb = Owner.Creature.CombatState;
        await CardPileCmd.AddGeneratedCardToCombat(
            cb.CreateCard<DeniaLookAtMe>(Owner), PileType.Hand, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(
            cb.CreateCard<DeniaPityMe>(Owner), PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<DeniaFirstLastGift>();
}
