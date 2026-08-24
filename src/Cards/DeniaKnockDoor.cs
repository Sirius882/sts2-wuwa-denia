using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>轻叩门扉 — Uncommon Skill, Exhaust. 升级提高失去力量数值。</summary>
public sealed class DeniaKnockDoor : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_knock_door.png";

    public DeniaKnockDoor() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "轻叩门扉",
            Description: "[color=#9A6A18]黑色形态[/color]：所有敌人失去{IfUpgraded:show:6|4}[color=#9A6A18]力量[/color]。\n[color=#9A6A18]粉色形态[/color]：目标失去{IfUpgraded:show:8|6}点[color=#9A6A18]力量[/color]。\n持续1回合。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            decimal amount = IsUpgraded ? 6m : 4m;
            foreach (var e in Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray())
                await PowerCmd.Apply<DeniaKnockDoorStrengthLossPower>(ctx, e, amount, Owner.Creature, this);
        }
        else
        {
            decimal amount = IsUpgraded ? 8m : 6m;
            await PowerCmd.Apply<DeniaKnockDoorStrengthLossPower>(ctx, play.Target, amount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
