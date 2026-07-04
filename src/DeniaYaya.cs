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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>？！娅娅？！ — Common Attack, 1 dmg, resonance break, black form returns to pink.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaYaya : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(1m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_yaya.png";

    public DeniaYaya()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "？！娅娅？！",
        Description: "造成{Damage:diff()}点伤害，触发无条件[gold]谐度破坏[/gold]。此次[gold]谐度破坏[/gold]只造成五分之一的伤害。若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]。\n虚质强化：再造成4点伤害2次。{IfUpgraded:show:\n保留。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        bool spentVirtualMatter = await TrySpendVirtualMatter(play);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        await DeniaResonanceBreakDamageModifier.RunOnce(
            0.2m,
            () => AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this));

        if (spentVirtualMatter)
        {
            for (int i = 0; i < 2; i++)
            {
                await DamageCmd.Attack(4m)
                    .FromCard(this)
                    .Targeting(play.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(ctx);
            }
        }

        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
