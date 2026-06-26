using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>吞没 — Uncommon Attack, 1e. 5/7 dmg x3 to single. VM: hits+1.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSwallow : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(5m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_swallow.png";

    public DeniaSwallow()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "吞没",
        Description: "造成{Damage:diff()}点伤害3次。\n虚质强化：次数+1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        decimal dmg = DynamicVars.Damage.BaseValue;
        int hits = 3;
        if (await TrySpendVirtualMatter(play))
            hits++;

        await DamageCmd.Attack(dmg)
            .WithHitCount(hits)
            .FromCard(this)
            .Targeting(play.Target)
            .Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
