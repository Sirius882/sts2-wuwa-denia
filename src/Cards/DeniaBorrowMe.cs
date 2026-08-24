using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>借我用下 — Rare Skill, 1e. Block per distinct buff/debuff type. VM: multiplier+1.</summary>
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
            Description: "敌人每有一种[color=#9A6A18]减益[/color]、自身每有一种[color=#9A6A18]增益[/color]，获得2点[color=#9A6A18]格挡[/color]。\n虚质强化：获得的[color=#9A6A18]格挡[/color]+1。");

    /// <summary>统计玩家身上的增益种类数（按 PowerModel 类型去重）。</summary>
    private static int CountBuffTypes(Creature player)
    {
        var types = new System.Collections.Generic.HashSet<System.Type>();
        foreach (var p in player.Powers)
        {
            if (p.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff && p.Amount > 0)
                types.Add(p.GetType());
        }
        return types.Count;
    }

    /// <summary>统计敌人身上的减益种类数（按 PowerModel 类型去重）。</summary>
    private static int CountDebuffTypes(Creature enemy)
    {
        var types = new System.Collections.Generic.HashSet<System.Type>();
        foreach (var p in enemy.Powers)
        {
            if ((p.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff
                    || p.GetType().Name.Contains("FusionBurstCap", StringComparison.Ordinal))
                && p.Amount > 0)
                types.Add(p.GetType());
        }
        return types.Count;
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int buffTypes = CountBuffTypes(Owner.Creature);
        int debuffTypes = 0;
        foreach (var enemy in Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead))
            debuffTypes += CountDebuffTypes(enemy);

        int multiplier = await TrySpendVirtualMatter(play) ? 3 : 2;
        int block = (buffTypes + debuffTypes) * multiplier;
        if (block > 0)
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block, ValueProp.Move), play);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
