using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>乖~ — Uncommon Attack</summary>
public sealed class DeniaBehave : DeniaCard
{
    public override int CurrentVirtualMatterCost => 5;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(15m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_behave.png";

    public DeniaBehave()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        int hitCount = 1;
        if (await TrySpendVirtualMatter(play))
            hitCount = 2;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hitCount)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_heavy_blunt").Execute(ctx);

        await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(5m); }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "乖~", Description: "造成{Damage:diff()}点伤害。\n获得2点[gold]力量[/gold]。\n虚质强化：再造成一段伤害。");
}
