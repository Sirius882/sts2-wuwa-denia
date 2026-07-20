using System;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaVisit : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_visit.png";

    public DeniaVisit() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "谨此致访",
        Description: "提高聚爆上限{IfUpgraded:show:3|2}。触发{IfUpgraded:show:3|2}次[gold]熔解[/gold]。附加上限{IfUpgraded:show:1/2|3/7}的[gold]聚爆[/gold]。\n虚质强化：此牌的[gold]熔解[/gold]不消耗聚爆层数。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int capInc = IsUpgraded ? 3 : 2;
        int melt = IsUpgraded ? 3 : 2;
        int num = IsUpgraded ? 1 : 3;
        int den = IsUpgraded ? 2 : 7;
        bool preserve = await TrySpendVirtualMatter(play);

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, capInc, Owner.Creature, this);
        using var scope = preserve ? DeniaMeltProtectPatch.BeginPreserve(this) : null;
        for (int i = 0; i < melt; i++)
        {
            if (play.Target.IsDead) break;
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
        }
        if (!play.Target.IsDead)
        {
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, num, den);
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
