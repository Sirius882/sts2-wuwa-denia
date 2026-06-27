using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>鱼罐头松糕 — Uncommon Attack, 1e. 易伤×2(升级×3)。VM强化:先+3再乘。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaCannedFishCake : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_canned_fish_cake.png";

    public DeniaCannedFishCake()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "鱼罐头松糕",
        Description: "目标身上的[gold]易伤[/gold]×{IfUpgraded:show:3|2}。\n虚质强化：先附加3[gold]易伤[/gold]，再结算乘算。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int multiplier = IsUpgraded ? 3 : 2;

        // 虚质强化：先+3易伤
        if (await TrySpendVirtualMatter(play))
            await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, 3, Owner.Creature, this);

        // 计算需要附加多少层才能达到 multiplier 倍
        int current = (int)(play.Target.GetPower<VulnerablePower>()?.Amount ?? 0);
        if (current <= 0) return;

        int targetAmount = current * multiplier;
        int toAdd = targetAmount - current;
        if (toAdd > 0)
            await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, toAdd, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
