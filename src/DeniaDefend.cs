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

public sealed class DeniaDefend : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new BlockVar(5m, ValueProp.Move) };
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_defend.png";

    public DeniaDefend()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    public override bool GainsBlock => true;

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "防御", Description: "获得{Block:diff()}点[gold]格挡[/gold]。若处于粉色形态，对随机一名敌人附加1点[gold]聚爆[/gold]。虚质强化：对随机敌人附加2点[gold]聚爆[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var enemy = DeniaFormHelper.PickRandomEnemy(Owner);
        bool vmSpent = await TrySpendVirtualMatter(play);
        if (vmSpent && enemy != null)
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 2, Owner.Creature, this);

        if (!vmSpent && DeniaFormHelper.IsPink(Owner.Creature) && enemy != null)
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
