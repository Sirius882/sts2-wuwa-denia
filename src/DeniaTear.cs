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

/// <summary>撕裂 — Common Attack</summary>
public sealed class DeniaTear : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, ValueProp.Move), new BlockVar(7m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_tear.png";

    public DeniaTear()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        int hits = IsUpgraded ? 3 : 2;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        if (await TrySpendVirtualMatter(play))
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "撕裂", Description: "获得{Block:diff()}点[gold]格挡[/gold]。造成{Damage:diff()}点伤害{IfUpgraded:show:3|2}次。\n虚质强化：[gold]熔解[/gold]1。");
}
