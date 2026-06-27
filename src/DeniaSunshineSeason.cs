#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>阳光季节 — Rare Skill, 1e(upg:0e). 切换形态，抽1张牌，获得1能量和1黯核。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSunshineSeason : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_sunshine_season.png";

    public DeniaSunshineSeason()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "阳光季节",
        Description: "切换形态。抽1张牌。获得1点能量和1黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
        else
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        await CardPileCmd.Draw(ctx, 1, Owner);
        await PlayerCmd.GainEnergy(1, Owner);
        await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
