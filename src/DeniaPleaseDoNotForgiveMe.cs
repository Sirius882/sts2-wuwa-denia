using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>请您不要···宽恕我 — Common Skill</summary>
public sealed class DeniaPleaseDoNotForgiveMe : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_please_do_not_forgive_me.png";

    public DeniaPleaseDoNotForgiveMe()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "请您不要···宽恕我",
        Description: "只在[gold]粉色[/gold]形态下有效。切换到[gold]黑色[/gold]形态，不获得「直视我」和「怜悯我」。获得2点[gold]力量[/gold]和20层[gold]聚爆轨迹[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsPink(Owner.Creature)) return;

        await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);

        await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, 2, Owner.Creature, this);
        await PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(
            ctx, Owner.Creature, 20, Owner.Creature, this);

        DeniaFormHelper.RecordForgiveMeStrength(Owner.Creature, 2);
        DeniaFormHelper.RecordForgiveMeTrajectory(Owner.Creature, 20);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
