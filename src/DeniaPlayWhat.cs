#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPlayWhat : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? new[] { TuneStrainKeywords.TuneStrainResponse }
        : new[] { CardKeyword.Exhaust, TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_play_what.png";

    public DeniaPlayWhat()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "玩什么？",
        Description: "附加1[gold]集谐·偏移[/gold]，造成{Damage:diff()}点伤害。{IfUpgraded:show:\n不再消耗。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
        AddKeyword(TuneStrainKeywords.TuneStrainResponse);
    }
}
