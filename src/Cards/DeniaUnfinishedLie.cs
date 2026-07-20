using System;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaUnfinishedLie : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_unfinished_lie.png";

    public DeniaUnfinishedLie() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "未竟的谎言",
        Description: "提高聚爆上限{IfUpgraded:show:3|2}。附加上限{IfUpgraded:show:61%|1/2}的[gold]聚爆[/gold]。若这张牌触发引爆，获得1点能量。\n虚质强化：附加的[gold]聚爆[/gold]层数和聚爆上限都+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int cu = IsUpgraded ? 3 : 2;
        int flatBurstBonus = 0;
        if (await TrySpendVirtualMatter(play))
        {
            cu += 2;
            flatBurstBonus += 2;
        }

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, cu, Owner.Creature, this);

        int ratioBurst = IsUpgraded
            ? DeniaFusionBurstMath.CeilPercentOfCap(play.Target, 61)
            : DeniaFusionBurstMath.CeilRatioOfCap(play.Target, 1, 2);
        int bu = ratioBurst + flatBurstBonus;

        int before = AemeathFusionBurstState.GetFusionBurst(play.Target);
        int cap = DeniaFusionBurstMath.GetCanonicalCap(play.Target);
        if (bu > 0)
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, bu, Owner.Creature, this);
        int after = AemeathFusionBurstState.GetFusionBurst(play.Target);
        bool burstTriggered = before + bu >= cap && after < before + bu;
        if (burstTriggered)
            await PlayerCmd.GainEnergy(1, Owner);
    }

    protected override void OnUpgrade() { }
}
