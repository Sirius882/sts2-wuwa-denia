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

/// <summary>轻叩门扉 — Common Skill, Exhaust</summary>
public sealed class DeniaKnockDoor : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_knock_door.png";

    public DeniaKnockDoor() : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "轻叩门扉",
        Description: "黑色形态：所有敌人失去4点[gold]力量[/gold]。\n粉色形态：目标失去6点[gold]力量[/gold]。\n持续1回合。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            foreach (var e in Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray())
                await PowerCmd.Apply<DeniaKnockDoorStrengthLossPower>(ctx, e, 4m, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<DeniaKnockDoorStrengthLossPower>(ctx, play.Target, 6m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
