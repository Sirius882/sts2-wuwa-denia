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

/// <summary>族群进化的错误 — Rare Attack: if bias then rupture (upg always after applying 1 bias); always deal 10 dmg; VM +10 dmg.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTribeEvolutionError : DeniaCard
{
    public override int CurrentVirtualMatterCost => 5;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_tribe_evolution_error.png";

    public DeniaTribeEvolutionError()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "族群进化的错误",
            Description: "{IfUpgraded:show:附加1[color=#9A6A18]集谐·偏移[/color]，无条件[color=#9A6A18]谐度破坏[/color]。|若目标带有[color=#9A6A18]集谐·偏移[/color]，无条件[color=#9A6A18]谐度破坏[/color]。}造成{Damage:diff()}点伤害。\n虚质强化：伤害+10。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        if (IsUpgraded)
        {
            await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
            if (!play.Target.IsDead)
                await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        }
        else if (TuneStrainState.GetBias(play.Target) > 0)
        {
            await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        }

        // 伤害无条件，不依赖前半句
        if (play.Target.IsDead) return;

        decimal damage = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            damage += 10m;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
    }

    protected override void OnUpgrade() { }
}
