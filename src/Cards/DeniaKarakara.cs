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

/// <summary>喀拉喀拉 — Rare Skill, 2e(upg:1). Gain STR = Vulnerable and stars = ceil(cap/5); VM swaps both bonuses.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaKarakara : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_karakara.png";

    public DeniaKarakara()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "喀拉喀拉",
            Description: "获得等同于目标身上易伤层数的力量、聚爆上限1/5的蔽星。\n虚质强化：再获得聚爆上限1/5的力量、易伤层数的蔽星。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int vuln = play.Target.GetPower<VulnerablePower>()?.Amount ?? 0;
        int capShare = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, 1, 5);
        if (vuln > 0)
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, vuln, Owner.Creature, this);
        if (capShare > 0)
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, capShare, Owner.Creature, this);
        if (!await TrySpendVirtualMatter(play)) return;
        if (capShare > 0)
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, capShare, Owner.Creature, this);
        if (vuln > 0)
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, vuln, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
