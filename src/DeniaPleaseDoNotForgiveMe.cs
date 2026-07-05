using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>请您不要···宽恕我 — Common Skill</summary>
public sealed class DeniaPleaseDoNotForgiveMe : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_please_do_not_forgive_me.png";

    public DeniaPleaseDoNotForgiveMe()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "请您不要···宽恕我",
        Description: "在[gold]粉色形态[/gold]下，切换到[gold]黑色形态[/gold]，不获得\"直视我\"和\"怜悯我\"，获得20层[gold]聚爆轨迹[/gold]并进入[gold]共鸣模态·集谐[/gold]。退出[gold]黑色形态[/gold]时，失去由此获得的聚爆轨迹并退出[gold]共鸣模态·集谐[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsPink(Owner.Creature)) return;

        await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this, addBlackFormCards: false);

        await PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(
            ctx, Owner.Creature, 20, Owner.Creature, this);
        await PowerCmd.Apply<DeniaResonanceModePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        await DeniaFormHelper.AddBlackFormTrajectoryDebt(Owner.Creature, 20, Owner.Creature, this);
        await DeniaFormHelper.MarkTemporaryResonanceMode(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
