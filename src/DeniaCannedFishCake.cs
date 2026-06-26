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

/// <summary>鱼罐头松糕 — Uncommon Attack, 1e. Double vuln. VM强化: +3 vuln after.</summary>
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
        Description: "翻倍目标身上的[gold]易伤[/gold]。{IfUpgraded:show:再附加2倍于身上[gold]易伤[/gold]的[gold]易伤[/gold]。|}\n虚质强化：先附加3[gold]易伤[/gold]，再结算翻倍。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        // 虚质强化：先+3易伤，再翻倍
        if (await TrySpendVirtualMatter(play))
            await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, 3, Owner.Creature, this);

        var vulnPower = play.Target.GetPower<VulnerablePower>();
        int currentVuln = (int)(vulnPower?.Amount ?? 0);
        if (currentVuln > 0)
            await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, currentVuln, Owner.Creature, this);

        // 升级后：再附加2倍于当前身上易伤的易伤
        if (IsUpgraded)
        {
            int currentAfter = (int)(play.Target.GetPower<VulnerablePower>()?.Amount ?? 0);
            if (currentAfter > 0)
                await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, currentAfter * 2, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
