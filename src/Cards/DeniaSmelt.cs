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
using TuneStrain;

namespace Denia;

/// <summary>熔毁 — Common Attack: block, melt 2, +2 burst; VM block+2.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSmelt : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(7m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_smelt.png";

    public DeniaSmelt()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    public override List<(string, string)>? Localization => new CardLoc(Title: "熔毁",
            Description: "获得{Block:diff()}点[color=#9A6A18]格挡[/color]，[color=#9A6A18]熔解[/color]2，附加2[color=#9A6A18]聚爆[/color]。\n虚质强化：格挡+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        decimal block = DynamicVars.Block.BaseValue;
        if (await TrySpendVirtualMatter(play))
            block += 2m;

        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block, ValueProp.Move), play);

        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 2);

        if (!play.Target.IsDead)
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 2, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
