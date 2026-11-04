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
            Description: "选择：令所有敌人失去{IfUpgraded:show:6|4}点力量，或令单个敌人失去{IfUpgraded:show:8|6}点力量。持续1回合。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool all = await DeniaTargetChoice.ChooseAll<DeniaStrengthSingleChoice, DeniaStrengthAllChoice>(ctx, this);
        int amount = all ? (IsUpgraded ? 6 : 4) : (IsUpgraded ? 8 : 6);
        var enemies = all
            ? Owner.Creature.CombatState.Enemies.Where(enemy => !enemy.IsDead).ToArray()
            : new[] { play.Target };
        foreach (var enemy in enemies)
            await PowerCmd.Apply<DeniaKnockDoorStrengthLossPower>(ctx, enemy, amount, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
