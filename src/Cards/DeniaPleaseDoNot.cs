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
public sealed class DeniaPleaseDoNot : DeniaCard, ITranscendenceCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_please_do_not.png";

    public DeniaPleaseDoNot()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "请您不要···",
            Description: "切换到[color=#9A6A18]黑色形态[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsPink(Owner.Creature))
            return;

        await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<DeniaImBack>();
}
