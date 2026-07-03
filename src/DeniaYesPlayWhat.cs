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
public sealed class DeniaYesPlayWhat : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? new[] { CardKeyword.Exhaust, CardKeyword.Retain, TuneStrainKeywords.TuneStrainResponse }
        : new[] { CardKeyword.Exhaust, TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_yes_play_what.png";

    public DeniaYesPlayWhat()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "是啊，玩什么？",
        Description: "无条件[gold]谐度破坏[/gold]。{IfUpgraded:show:\n获得保留。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        ArgumentNullException.ThrowIfNull(play.Target);
        await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
