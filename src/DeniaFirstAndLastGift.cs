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
public sealed class DeniaFirstAndLastGift : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_first_last_gift.png";

    public DeniaFirstAndLastGift()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "最初和最后的礼物",
        Description: "附加2[gold]集谐·偏移[/gold]，触发无条件[gold]谐度破坏[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        ArgumentNullException.ThrowIfNull(play.Target);

        await TuneStrainState.TryAddBias(play.Target, 2, Owner.Creature, this);
        await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}