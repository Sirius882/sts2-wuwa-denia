#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
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
public sealed class DeniaPommelStrike : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(8m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_pommel_strike.jpg";

    public DeniaPommelStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "剑柄打击",
        Description: "造成{Damage:diff()}点伤害。若此牌打出前目标已有[gold]集谐·偏移[/gold]，再附加1点[gold]集谐·偏移[/gold]{IfUpgraded:show:，然后触发无条件[gold]谐度破坏[/gold]|}。\n虚质强化：最后再造成10点伤害一次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        bool hadBias = TuneStrainState.GetBias(play.Target) > 0;
        bool vmEnhanced = await TrySpendVirtualMatter(play);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        if (hadBias)
        {
            await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
            if (IsUpgraded)
                await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        }

        if (vmEnhanced)
        {
            await DamageCmd.Attack(10m)
                .FromCard(this)
                .Targeting(play.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(ctx);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(-2m);
    }
}