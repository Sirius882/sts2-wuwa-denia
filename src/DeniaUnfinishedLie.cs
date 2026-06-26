using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>未竟的谎言 — Uncommon Attack</summary>
public sealed class DeniaUnfinishedLie : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_unfinished_lie.png";

    public DeniaUnfinishedLie() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "未竟的谎言",
        Description: "提高聚爆上限{IfUpgraded:show:5|4}。\n附加{IfUpgraded:show:8|6}点[gold]聚爆[/gold]。\n若触发[gold]引爆[/gold]，获得1点能量。\n虚质强化：附加的[gold]聚爆[/gold]层数和上限各+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int cap = AemeathFusionBurstState.GetFusionBurstCap(play.Target);
        int cur = AemeathFusionBurstState.GetFusionBurst(play.Target);
        int cu = IsUpgraded ? 5 : 4;
        int bu = IsUpgraded ? 8 : 6;

        if (await TrySpendVirtualMatter(play)) { cu += 2; bu += 2; }

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, cu, Owner.Creature, this);
        bool willBurst = cur + bu >= cap + cu;
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, bu, Owner.Creature, this);
        if (willBurst) await PlayerCmd.GainEnergy(1, Owner);
    }

    protected override void OnUpgrade() { }
}
