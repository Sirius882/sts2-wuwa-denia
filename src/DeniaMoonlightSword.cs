using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>亚杜拉的月光剑 — 特殊来源无色技能</summary>
[Pool(typeof(ColorlessCardPool))]
public sealed class DeniaMoonlightSword : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_moonlight_sword.png";

    public DeniaMoonlightSword()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "亚杜拉的月光剑",
            Description: "令所有没有[gold]冻伤[/gold]的敌人失去其生命上限20%的血量。\n对所有敌人附加[gold]冻伤[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToList();

        // 1. 没有冻伤的敌人直接失去最大HP 20%的血量（不受力量、格挡等影响）
        foreach (var enemy in enemies)
        {
            if (enemy.GetPower<DeniaFrostbitePower>() != null) continue;
            int loss = enemy.MaxHp * 20 / 100;
            if (loss > 0)
            {
                enemy.LoseHpInternal(loss, ValueProp.Unblockable | ValueProp.Unpowered);
                var runState = enemy.Player?.RunState ?? enemy.CombatState.RunState;
                await Hook.AfterCurrentHpChanged(runState, enemy.CombatState, enemy, -(decimal)loss);
            }
        }

        // 2. 对所有敌人附加冻伤
        foreach (var enemy in enemies)
        {
            if (enemy.GetPower<DeniaFrostbitePower>() == null)
                await PowerCmd.Apply<DeniaFrostbitePower>(ctx, enemy, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
