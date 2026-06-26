using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>祝愿你于静默中 — Common Skill</summary>
public sealed class DeniaWishSilence : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_wish_silence.png";

    public DeniaWishSilence() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "祝愿你于静默中",
        Description: "触发{IfUpgraded:show:2|1}次[gold]熔解[/gold]。\n虚质强化：再触发1次[gold]熔解[/gold]，此卡触发的[gold]熔解[/gold]都不消耗聚爆层数。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int baseMelt = IsUpgraded ? 2 : 1;
        bool preserveBurst = await TrySpendVirtualMatter(play);
        int vmMelt = preserveBurst ? 1 : 0;
        int totalMelt = baseMelt + vmMelt;

        if (preserveBurst)
        {
            int beforeBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, totalMelt);
            int afterBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
            int lost = beforeBurst - afterBurst;
            if (lost > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, lost, Owner.Creature, this);
        }
        else
        {
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, totalMelt);
        }
    }

    protected override void OnUpgrade() { }
}
