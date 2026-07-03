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
public sealed class DeniaUe4ClientGameCrashed : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_ue4_client_game_crashed.png";

    public DeniaUe4ClientGameCrashed()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "UE4 Client Game已崩溃",
        Description: "附加{IfUpgraded:show:2|1}点[gold]集谐·偏移[/gold]。触发无条件[gold]谐度破坏[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        ArgumentNullException.ThrowIfNull(play.Target);

        await TuneStrainState.TryAddBias(play.Target, IsUpgraded ? 2 : 1, Owner.Creature, this);
        await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}