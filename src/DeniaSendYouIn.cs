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

/// <summary>送你进去 — Uncommon Attack, 1e. 3/5 dmg x3 to all. VM: hits+1.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSendYouIn : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(3m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_send_you_in.png";

    public DeniaSendYouIn()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "送你进去",
        Description: "对所有敌人造成{Damage:diff()}点伤害3次。\n虚质强化：次数+1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        decimal dmg = DynamicVars.Damage.BaseValue;
        int hits = 3;
        if (await TrySpendVirtualMatter(play))
            hits++;

        await DamageCmd.Attack(dmg)
            .WithHitCount(hits)
            .FromCard(this)
            .TargetingAllOpponents(Owner.Creature.CombatState)
            .Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
