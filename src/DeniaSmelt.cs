#nullable enable
using System;
using System.Collections.Generic;
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

/// <summary>熔毁 — Common Attack, 1e. 7 block, 5 dmg, melt 1.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSmelt : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(5m, ValueProp.Move), new BlockVar(7m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_smelt.png";

    public DeniaSmelt()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "熔毁",
        Description: "获得{Block:diff()}点[gold]格挡[/gold]。造成{Damage:diff()}点伤害，[gold]熔解[/gold]{IfUpgraded:show:2|1}。\n虚质强化：[gold]熔解[/gold]1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        int meltCount = IsUpgraded ? 2 : 1;
        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, meltCount);

        if (await TrySpendVirtualMatter(play))
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
