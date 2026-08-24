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

/// <summary>幻灭之形 — Uncommon Attack</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPhantomForm : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_phantom_form.png";

    public DeniaPhantomForm()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        bool vm = await TrySpendVirtualMatter(play);
        // 附加上限 1/2 的聚爆；虚质强化 +3 固定层
        int burstAdd = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, 1, 2);
        if (vm) burstAdd += 3;

        int cap = DeniaFusionBurstMath.GetCanonicalCap(play.Target);
        int current = AemeathFusionBurstState.GetFusionBurst(play.Target);
        bool willBurst = current + burstAdd >= cap;

        if (burstAdd > 0)
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burstAdd, Owner.Creature, this);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        if (willBurst)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "幻灭之形",
            Description: "附加上限1/2的[color=#9A6A18]聚爆[/color]，获得{Block:diff()}点[color=#9A6A18]格挡[/color]。如果这张牌触发[color=#9A6A18]引爆[/color]，再获得{Block:diff()}点[color=#9A6A18]格挡[/color]。\n虚质强化：附加的[color=#9A6A18]聚爆[/color]层数+3。");
}
