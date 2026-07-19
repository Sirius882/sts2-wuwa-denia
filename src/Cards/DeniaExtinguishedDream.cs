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

/// <summary>
/// 灭却之梦 — Common Skill。获得格挡并附加 1 集谐·偏移。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaExtinguishedDream : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(10m, ValueProp.Move) };

    public override bool GainsBlock => true;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_extinguished_dream.png";

    public DeniaExtinguishedDream()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "灭却之梦",
        Description: "获得{Block:diff()}点[gold]格挡[/gold]，附加1[gold]集谐·偏移[/gold]。\n虚质强化：格挡+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        decimal block = DynamicVars.Block.BaseValue;
        if (await TrySpendVirtualMatter(play))
            block += 5m;

        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block, ValueProp.Move), play);
        await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
