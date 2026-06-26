using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>泡沫蜜饵 — Common Attack AOE, 1e. 5 dmg 2/3 times. VM: hits+1.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFoamBait : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_foam_bait.png";

    public DeniaFoamBait()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "泡沫蜜饵",
            Description: "对所有敌人造成5点伤害{IfUpgraded:show:3|2}次。\n虚质强化：次数+1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int hits = IsUpgraded ? 3 : 2;
        if (await TrySpendVirtualMatter(play))
            hits++;

        for (int i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(5m)
                .FromCard(this).TargetingAllOpponents(Owner.Creature.CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(ctx);
        }
    }

    protected override void OnUpgrade() { }
}
