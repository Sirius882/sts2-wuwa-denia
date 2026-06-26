using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

public sealed class DeniaStrike : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(5m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_strike.png";

    public DeniaStrike()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "打击", Description: "造成{Damage:diff()}点伤害。\n[gold]粉色[/gold]：熔解1。\n虚质强化：基础伤害+4。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        var dmg = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            dmg += 4m;

        await DamageCmd.Attack(dmg)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        if (DeniaFormHelper.IsPink(Owner.Creature))
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
