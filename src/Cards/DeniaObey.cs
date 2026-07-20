using System;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>听话 — Rare Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaObey : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_obey.png";

    public DeniaObey()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        int melt = IsUpgraded ? 4 : 2;
        if (await TrySpendVirtualMatter(play))
            melt += 2;

        using var scope = DeniaMeltProtectPatch.BeginPreserve(this);
        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, melt);

        if (!play.Target.IsDead)
        {
            // 先上限后比例层数
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, 3, Owner.Creature, this);
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, 1, 2);
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
        }
    }

    public override System.Collections.Generic.List<(string, string)>? Localization =>
        new CardLoc(Title: "听话",
            Description: "对目标触发{IfUpgraded:show:4|2}次[gold]熔解[/gold]，此牌的[gold]熔解[/gold]不消耗聚爆层数。给目标附加3点[gold]聚爆[/gold]上限、上限1/2的[gold]聚爆[/gold]。\n虚质强化：多触发2次[gold]熔解[/gold]。");
}
