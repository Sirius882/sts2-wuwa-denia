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
public sealed class DeniaPhantomForm : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(8m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_phantom_form.png";

    public DeniaPhantomForm()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        int cap = AemeathFusionBurstState.GetFusionBurstCap(play.Target);
        int current = AemeathFusionBurstState.GetFusionBurst(play.Target);
        int burstAdd = 6;
        bool willBurst = current + burstAdd >= cap;

        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burstAdd, Owner.Creature, this);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        if (willBurst)
        {
            int block2 = IsUpgraded ? 10 : 8;
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block2, ValueProp.Move), play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "幻灭之形",
            Description: "附加6点[gold]聚爆[/gold]。获得{Block:diff()}点[gold]格挡[/gold]。\n若触发[gold]引爆[/gold]，再获得{IfUpgraded:show:10|8}点[gold]格挡[/gold]。");
}
