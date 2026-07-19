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

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "撕裂", Description: "获得{Block:diff()}点[gold]格挡[/gold]，造成{IfUpgraded:show:12|8}点伤害。\n虚质强化：伤害+4。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        bool vm = await TrySpendVirtualMatter(play);
        int totalDmg = IsUpgraded ? 12 : 8;
        if (vm) totalDmg += 4;

        await DamageCmd.Attack(totalDmg)
            .WithHitCount(1)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
