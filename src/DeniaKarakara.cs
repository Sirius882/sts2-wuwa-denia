using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>喀拉喀拉 — Rare Skill, 2e(upg:1). Gain str = enemy vuln. VM强化: gain str = enemy burst.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaKarakara : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_karakara.png";

    public DeniaKarakara()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "喀拉喀拉",
        Description: "获得等同于目标[gold]易伤[/gold]层数一半的[gold]力量[/gold]。\n虚质强化：获得等同于目标[gold]聚爆上限[/gold]一半的[gold]力量[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int vulnAmount = (int)(play.Target.GetPower<VulnerablePower>()?.Amount ?? 0) / 2;
        if (vulnAmount > 0)
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, vulnAmount, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
        {
            int burstCap = AemeathFusionBurstState.GetFusionBurstCap(play.Target) / 2;
            if (burstCap > 0)
                await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, burstCap, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
