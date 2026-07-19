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
public sealed class DeniaTribeEvolutionError : DeniaCard
{
    public override int CurrentVirtualMatterCost => 5;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_tribe_evolution_error.png";

    public DeniaTribeEvolutionError()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "族群进化的错误",
        Description: "造成{Damage:diff()}点伤害，附加{IfUpgraded:show:2|1}点[gold]集谐·偏移[/gold]。\n虚质强化：伤害+10。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        decimal damage = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            damage += 10m;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
        await TuneStrainState.TryAddBias(play.Target, IsUpgraded ? 2 : 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}