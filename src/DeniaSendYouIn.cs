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

/// <summary>送你进去 — Uncommon Attack, 1e. 9/15 to all. VM: +5 total damage.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSendYouIn : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_send_you_in.png";

    public DeniaSendYouIn()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "送你进去",
        Description: "对所有敌人造成{IfUpgraded:show:15|9}点伤害。\n虚质强化：伤害+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int baseDmg = IsUpgraded ? 15 : 9;
        if (await TrySpendVirtualMatter(play))
            baseDmg += 5;

        await DamageCmd.Attack(baseDmg)
            .WithHitCount(1)
            .FromCard(this)
            .TargetingAllOpponents(Owner.Creature.CombatState)
            .Execute(ctx);
    }

    protected override void OnUpgrade() { }
}
