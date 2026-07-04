#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFatalError : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fatal_error.png";

    public DeniaFatalError()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "Fatal Error!",
        Description: "切换形态。对目标附加{IfUpgraded:show:2|1}点[gold]集谐·偏移[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        if (DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
        else
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        await TuneStrainState.TryAddBias(play.Target, IsUpgraded ? 2 : 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}