using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>听话 — Rare Skill</summary>
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

        int melt = IsUpgraded ? 2 : 1;
        bool preserveBurst = await TrySpendVirtualMatter(play);
        if (preserveBurst) melt += 1;

        int beforeBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, melt);

        if (preserveBurst)
        {
            int afterBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
            int lost = beforeBurst - afterBurst;
            if (lost > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, lost, Owner.Creature, this);
        }

        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 10, Owner.Creature, this);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "听话",
            Description: "对目标触发{IfUpgraded:show:2|1}次[gold]熔解[/gold]。\n给目标附加10点[gold]聚爆[/gold]。\n虚质强化：多触发1次[gold]熔解[/gold]，此卡的[gold]熔解[/gold]不消耗聚爆层数。");
}
