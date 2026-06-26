using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>谨此致访 — Uncommon Attack</summary>
public sealed class DeniaVisit : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_visit.png";

    public DeniaVisit() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "谨此致访",
        Description: "提高聚爆上限{IfUpgraded:show:5|4}。\n触发{IfUpgraded:show:2|1}次[gold]熔解[/gold]。\n附加{IfUpgraded:show:8|6}点[gold]聚爆[/gold]。\n虚质强化：此牌的[gold]熔解[/gold]不消耗聚爆层数。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int capInc = IsUpgraded ? 5 : 4;
        int burst = IsUpgraded ? 8 : 6;
        int meltTimes = IsUpgraded ? 2 : 1;

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, capInc, Owner.Creature, this);

        bool preserveBurst = await TrySpendVirtualMatter(play);

        for (int i = 0; i < meltTimes; i++)
        {
            int before = AemeathFusionBurstState.GetFusionBurst(play.Target);
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
            if (preserveBurst && before > 0)
            {
                int after = AemeathFusionBurstState.GetFusionBurst(play.Target);
                int lost = before - after;
                if (lost > 0)
                    await AemeathFusionBurstState.TryAddFusionBurst(play.Target, lost, Owner.Creature, this);
            }
        }

        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
