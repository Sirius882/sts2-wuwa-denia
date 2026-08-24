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
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_karakara.png";

    public DeniaKarakara()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "喀拉喀拉",
            Description: "获得等同于目标[color=#9A6A18]易伤[/color]层数三分之一的[color=#9A6A18]力量[/color]。\n虚质强化：获得等同于目标[color=#9A6A18]聚爆上限[/color]三分之二的[color=#9A6A18]力量[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int vulnAmount = (int)(play.Target.GetPower<VulnerablePower>()?.Amount ?? 0) / 3;
        if (vulnAmount > 0)
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, vulnAmount, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
        {
            // 聚爆上限三分之二的力量：按 5+额外上限 向上取整
            int strFromCap = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, 2, 3);
            if (strFromCap > 0)
                await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, strFromCap, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
