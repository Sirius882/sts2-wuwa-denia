#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>你也试试？ — Rare Attack, 0e. This turn, each card you play adds 1(2) burst cap to target.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaYouTryIt : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_you_try_it.png";

    public DeniaYouTryIt()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "你也试试？",
            Description: "打出此牌后，本回合内你每打出一张牌，给该敌人附加{IfUpgraded:show:2|1}点[color=#9A6A18]聚爆上限[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int amount = IsUpgraded ? 2 : 1;
        await PowerCmd.Apply<DeniaYouTryItPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
        int targetIndex = Owner.Creature.CombatState.Enemies
            .Select((enemy, index) => (enemy, index))
            .FirstOrDefault(pair => ReferenceEquals(pair.enemy, play.Target)).index;
        bool targetFound = Owner.Creature.CombatState.Enemies.Any(enemy => ReferenceEquals(enemy, play.Target));
        if (targetFound)
        {
            await PowerCmd.Remove<DeniaYouTryItTargetIndexPower>(Owner.Creature);
            await PowerCmd.Apply<DeniaYouTryItTargetIndexPower>(ctx, Owner.Creature, targetIndex + 1, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaYouTryItPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "你也试试？",
        Description: "本回合内每打出一张牌，给目标敌人附加聚爆上限。",
        SmartDescription: "本回合内每打出一张牌，给目标敌人附加{Amount}聚爆上限。");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        await PowerCmd.Remove<DeniaYouTryItPower>(Owner);
        await PowerCmd.Remove<DeniaYouTryItTargetIndexPower>(Owner);
    }

    public static async Task OnAnyCardPlayed(Player player, CardPlay cardPlay)
    {
        var creature = player.Creature;
        var power = creature.GetPower<DeniaYouTryItPower>();
        var targetIndexPower = creature.GetPower<DeniaYouTryItTargetIndexPower>();
        if (power == null || targetIndexPower == null || targetIndexPower.Amount <= 0) return;
        if (cardPlay.Card is DeniaYouTryIt) return;
        int targetIndex = targetIndexPower.Amount - 1;
        var enemies = creature.CombatState.Enemies;
        if (targetIndex < 0 || targetIndex >= enemies.Count) return;
        var target = enemies[targetIndex];
        if (target.IsDead) return;
        int amount = power.Amount;
        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(target, amount, creature, null!);
    }
}
