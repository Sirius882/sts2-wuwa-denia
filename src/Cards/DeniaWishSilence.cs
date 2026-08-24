using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>祝愿你于静默中 — Common Attack, cost 0</summary>
public sealed class DeniaWishSilence : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_wish_silence.png";

    public DeniaWishSilence() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "祝愿你于静默中",
            Description: "[color=#9A6A18]熔解[/color]{IfUpgraded:show:3|2}。\n虚质强化：[color=#9A6A18]熔解[/color]2。此牌触发的[color=#9A6A18]熔解[/color]都不消耗聚爆层数。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int baseMelt = IsUpgraded ? 3 : 2;
        bool preserveBurst = await TrySpendVirtualMatter(play);
        int vmMelt = preserveBurst ? 2 : 0;
        int totalMelt = baseMelt + vmMelt;

        using var scope = preserveBurst ? DeniaMeltProtectPatch.BeginPreserve(this) : null;
        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, totalMelt);
    }

    protected override void OnUpgrade() { }
}