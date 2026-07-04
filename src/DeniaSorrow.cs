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
public sealed class DeniaSorrow : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_sorrow.png";

    public DeniaSorrow()
        : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "哀",
        Description: "造成{Damage:diff()}点伤害，附加{IfUpgraded:show:2|1}点[gold]集谐·偏移[/gold]，触发无条件[gold]谐度破坏[/gold]。此次[gold]谐度破坏[/gold]只造成五分之一的伤害。\n虚质强化：伤害+4。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        decimal damage = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            damage += 4m;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        await TuneStrainState.TryAddBias(play.Target, IsUpgraded ? 2 : 1, Owner.Creature, this);
        await DeniaResonanceBreakDamageModifier.RunOnce(
            0.2m,
            () => AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this));
    }

    protected override void OnUpgrade() { }
}