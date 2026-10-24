using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 蚀刻木桩：受到的聚爆引爆伤害与谐度破坏伤害翻倍（谐度破坏翻倍在
/// DeniaCrushDistributionPatch 注册的百分比修正器里实现）。
/// Amount = 战斗胜利且被玩家击杀时全队恢复的生命值。
/// 击杀标记与胜利结算都发生在木桩死亡之后，因此 power 需在持有者死亡后保留。
/// </summary>
public sealed class DeniaEtchingStakePower : CustomPowerModel
{
    private bool _killedByPlayers;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_etching_stake_power.png";

    public override string? CustomBigIconPath => "res://images/ui/powers/denia_etching_stake_power.png";

    // 死亡后保留：胜利结算与击杀标记都在死亡之后发生。
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "蚀刻木桩",
            Description: "受到的[color=#9A6A18]聚爆引爆[/color]伤害和[color=#9A6A18]谐度破坏[/color]伤害翻倍。本场战斗胜利时，若其被玩家击杀而非死于爪牙效果，所有玩家恢复{Amount}点生命。",
            SmartDescription: "受到的聚爆引爆伤害和谐度破坏伤害翻倍。被玩家击杀则战斗胜利时所有玩家恢复{Amount}点生命。");

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只翻倍聚爆引爆伤害（熔解不算）；轧碎集中结算落地的伤害已含本倍率，不再重复乘。
        if (target != Owner
            || !AemeathFusionBurstState.IsDetonationProcessing
            || AemeathFusionBurstState.IsConcentratedDetonationDelivery)
        {
            return 1m;
        }

        return 2m;
    }

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        // 击杀判定走 AfterDamageGiven：它在 Kill（含死亡清 power）之前触发，
        // 且致死一击不会再走 AfterDamageReceived。爪牙清扫处死不经过伤害事件，不会置位。
        if (target == Owner && result.WasTargetKilled)
        {
            _killedByPlayers = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        // _killedByPlayers 不参与存档：战斗内保存/读档会丢失该标记，属可接受边缘情况。
        if (!_killedByPlayers || Amount <= 0)
        {
            return;
        }

        foreach (var player in Owner.CombatState?.Players ?? [])
        {
            if (player.Creature.IsDead)
            {
                continue;
            }

            await CreatureCmd.Heal(player.Creature, Amount);
        }
    }
}
