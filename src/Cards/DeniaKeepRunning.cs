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

/// <summary>继续逃啊？ — Rare Attack。本回合每打牌按目标上限比例附加聚爆。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaKeepRunning : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_keep_running.png";

    public DeniaKeepRunning()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "继续逃啊？",
            Description: "打出此牌后，本回合内你每打出一张牌，给该敌人附加上限{IfUpgraded:show:1/3|1/4}的[color=#9A6A18]聚爆[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        // Amount = 比例分母：未升级 1/4，升级 1/3
        int ratioDenom = IsUpgraded ? 3 : 4;
        await PowerCmd.Apply<DeniaKeepRunningPower>(ctx, Owner.Creature, ratioDenom, Owner.Creature, this);
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
    // Amount = 比例分母（4 或 3）
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "继续逃啊？",
        Description: "本回合内每打出一张牌，按目标聚爆上限比例附加聚爆。",
        SmartDescription: "本回合内每打出一张牌，给目标敌人附加上限1/{Amount}的聚爆。");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        await PowerCmd.Remove<DeniaKeepRunningPower>(Owner);
        await PowerCmd.Remove<DeniaKeepRunningTargetIndexPower>(Owner);
    }

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
        int ratioDenom = Math.Max(1, power.Amount);
        int amount = DeniaFusionBurstMath.CeilRatioOfCap(target, 1, ratioDenom);
        if (amount <= 0) return;
        await AemeathFusionBurstState.TryAddFusionBurst(target, amount, creature, null!);
    }
}
