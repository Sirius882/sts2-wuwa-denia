using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>松针岩绒卷 — Uncommon Attack, 1e. 4 vuln. VM强化: +2 str.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPineNeedleRockFloss : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(4m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_pine_needle_rock_floss.png";

    public DeniaPineNeedleRockFloss()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "松针岩绒卷",
        Description: "对目标附加{IfUpgraded:show:6|4}层[gold]易伤[/gold]。\n虚质强化：获得2点[gold]力量[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int vulnAmount = IsUpgraded ? 6 : 4;
        await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, vulnAmount, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, 2, Owner.Creature, this);

        // 升级后额外造成4点伤害
        if (IsUpgraded)
            await DamageCmd.Attack(4m)
                .FromCard(this)
                .Targeting(play.Target)
                .Execute(ctx);
    }

    protected override void OnUpgrade() { }
}
