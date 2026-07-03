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

/// <summary>继续逃啊？ — Rare Attack, 0e. This turn, each card you play adds 3 burst to target.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaKeepRunning : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_keep_running.png";

    public DeniaKeepRunning()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "继续逃啊？",
        Description: "打出此牌后，本回合内你每打出一张牌，给该敌人附加{IfUpgraded:show:4|2}[gold]聚爆[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int amount = IsUpgraded ? 4 : 2;
        await PowerCmd.Apply<DeniaKeepRunningPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
        int targetIndex = Owner.Creature.CombatState.Enemies
            .Select((enemy, index) => (enemy, index))
            .FirstOrDefault(pair => ReferenceEquals(pair.enemy, play.Target)).index;
        bool targetFound = Owner.Creature.CombatState.Enemies.Any(enemy => ReferenceEquals(enemy, play.Target));
        if (targetFound)
        {
            await PowerCmd.Remove<DeniaKeepRunningTargetIndexPower>(Owner.Creature);
            await PowerCmd.Apply<DeniaKeepRunningTargetIndexPower>(ctx, Owner.Creature, targetIndex + 1, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaKeepRunningPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "继续逃啊？",
            Description: "本回合内每打出一张牌，给目标敌人附加聚爆。",
            SmartDescription: "本回合内每打出一张牌，给目标敌人附加{Amount}聚爆。");

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            _ = PowerCmd.Remove<DeniaKeepRunningPower>(Owner);
            _ = PowerCmd.Remove<DeniaKeepRunningTargetIndexPower>(Owner);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每打出一张牌时调用：给目标附加聚爆。
    /// 必须 await（调用方在异步 Hook 包装链中 await），否则 fire-and-forget 会导致多人 desync。
    /// </summary>
    public static async Task OnAnyCardPlayed(Player player, CardPlay cardPlay)
    {
        var creature = player.Creature;
        var power = creature.GetPower<DeniaKeepRunningPower>();
        var targetIndexPower = creature.GetPower<DeniaKeepRunningTargetIndexPower>();
        if (power == null || targetIndexPower == null || targetIndexPower.Amount <= 0) return;
        if (cardPlay.Card is DeniaKeepRunning) return;
        int targetIndex = targetIndexPower.Amount - 1;
        var enemies = creature.CombatState.Enemies;
        if (targetIndex < 0 || targetIndex >= enemies.Count) return;
        var target = enemies[targetIndex];
        if (target.IsDead) return;
        int amount = power.Amount;
        await AemeathFusionBurstState.TryAddFusionBurst(target, amount, creature, null!);
    }
}
