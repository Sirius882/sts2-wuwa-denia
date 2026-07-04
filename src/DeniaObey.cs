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

        int melt = IsUpgraded ? 4 : 2;
        if (await TrySpendVirtualMatter(play))
            melt += 2;

        DeniaMeltProtectPatch.PreserveNextMelt = true;
        try
        {
            await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, melt);
        }
        finally { DeniaMeltProtectPatch.PreserveNextMelt = false; }

        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 10, Owner.Creature, this);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "听话",
            Description: "对目标触发{IfUpgraded:show:4|2}次[gold]熔解[/gold]，此次[gold]熔解[/gold]不消耗聚爆层数。\n给目标附加10点[gold]聚爆[/gold]。\n虚质强化：多触发2次[gold]熔解[/gold]，此卡的[gold]熔解[/gold]不消耗聚爆层数。");
}
