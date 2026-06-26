using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>借我用下 — Rare Skill, 1e. Block per buff+debuff. VM: multiplier+1.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaBorrowMe : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_borrow_me.png";

    public DeniaBorrowMe()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "借我用下",
            Description: "敌人每有一层减益、自身每有一层增益，获得2点[gold]格挡[/gold]。\n虚质强化：额外获得1格挡。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int buffs = DeniaBuffTracker.CountPlayerBuffs(Owner.Creature);
        int debuffs = 0;
        foreach (var enemy in Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead))
            debuffs += DeniaBuffTracker.CountEnemyDebuffs(enemy);

        int multiplier = await TrySpendVirtualMatter(play) ? 3 : 2;
        int block = (buffs + debuffs) * multiplier;
        if (block > 0)
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block, ValueProp.Move), play);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
