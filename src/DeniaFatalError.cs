#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFatalError : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fatal_error.png";

    public DeniaFatalError()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "Fatal Error!",
        Description: "切换形态。对目标附加{IfUpgraded:show:6|4}点[gold]偏谐[/gold]。\n虚质强化：附加[gold]偏谐[/gold]+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        bool spentVirtualMatter = await TrySpendVirtualMatter(play);

        if (DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
        else
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        int offTune = IsUpgraded ? 6 : 4;
        if (spentVirtualMatter)
            offTune += 2;
        await AemeathMechanicsApi.TryAddOffTune(play.Target, offTune, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}