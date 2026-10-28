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
using TuneStrain.Powers;

namespace Denia;

/// <summary>薄荷糖：夏日海滩事件专属攻击。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaMintCandy : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_mint_candy.jpg";

    public DeniaMintCandy()
        : base(2, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy, showInCardLibrary: true) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "薄荷糖",
        Description: "造成{Damage:diff()}点伤害，然后自己获得20[color=#9A6A18]集谐响应[/color]，给目标附加2[color=#9A6A18]集谐·干涉[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        await PowerCmd.Apply<TuneStrainResponsePower>(ctx, Owner.Creature, 20, Owner.Creature, this);
        await PowerCmd.Remove<TuneStrainResponseDurationPower>(Owner.Creature);
        await PowerCmd.Apply<TuneStrainResponseDurationPower>(ctx, Owner.Creature, 3, Owner.Creature, this);

        if (!play.Target.IsDead)
        {
            await PowerCmd.Apply<TuneStrainInterferencePower>(ctx, play.Target, 2, Owner.Creature, this);
            await PowerCmd.Remove<TuneStrainInterferenceDurationPower>(play.Target);
            await PowerCmd.Apply<TuneStrainInterferenceDurationPower>(ctx, play.Target, 3, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
