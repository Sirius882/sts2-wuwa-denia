#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 污涌之暗 — Rare Attack。立即 7 点伤害 3 次，然后 7 回合结束各打 5 点；
/// 期间若玩家受到未被格挡的伤害则取消后续。黯核强化每段基础伤害 +2。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTaintedDarkness : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(7m, ValueProp.Move),
            new DamageVar("DelayedDamage", 5m, ValueProp.Move),
        };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_tainted_darkness.png";

    public DeniaTaintedDarkness()
        : base(4, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "污涌之暗",
        Description:
            "造成{Damage:diff()}点伤害3次。在你接下来的7回合，在回合结束时，对该目标造成{DelayedDamage:diff()}点伤害。" +
            "但如果期间你受到了未被格挡的伤害，则取消后续的攻击。" +
            "\n黯核强化：每一段伤害的基础数值都+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int bonus = await TrySpendDarkCore(play) ? 2 : 0;
        decimal immediate = DynamicVars.Damage.BaseValue + bonus;
        decimal delayed = DynamicVars["DelayedDamage"].BaseValue + bonus;

        await DamageCmd.Attack(immediate)
            .WithHitCount(3)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        if (play.Target.IsDead) return;

        int targetIndex = Owner.Creature.CombatState.Enemies
            .Select((enemy, index) => (enemy, index))
            .FirstOrDefault(pair => ReferenceEquals(pair.enemy, play.Target)).index;
        bool targetFound = Owner.Creature.CombatState.Enemies
            .Any(enemy => ReferenceEquals(enemy, play.Target));
        if (!targetFound) return;

        // 用独立实例记录：剩余回合 / 伤害 / 目标索引（多人安全）。
        var power = await PowerCmd.Apply<DeniaTaintedDarknessPower>(
            ctx, Owner.Creature, 7m, Owner.Creature, this);
        power?.Configure(delayed, targetIndex + 1);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

/// <summary>
/// 污涌之暗延迟伤害实例。Amount = 剩余回合数。
/// DynamicVars: Damage=每段伤害, TargetIndex=敌人列表 1-based 索引。
/// </summary>
public sealed class DeniaTaintedDarknessPower : CustomPowerModel
{
    private const string TargetIndexKey = "TargetIndex";

    static DeniaTaintedDarknessPower() =>
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaTaintedDarknessPower));

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(5m, ValueProp.Move),
            new DynamicVar(TargetIndexKey, 0m),
        };

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "污涌之暗",
        Description: "接下来{Amount}回合，在你的回合结束时对目标造成伤害。若你受到未被格挡的伤害，取消后续攻击。",
        SmartDescription: "接下来{Amount}回合，在你的回合结束时对目标造成{Damage}点伤害。若你受到未被格挡的伤害，取消后续攻击。");

    public void Configure(decimal damage, int targetIndex1Based)
    {
        AssertMutable();
        DynamicVars.Damage.BaseValue = damage;
        DynamicVars[TargetIndexKey].BaseValue = targetIndex1Based;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        if (!participants.Contains(Owner)) return;
        if (Amount <= 0) return;

        Creature? target = ResolveTarget();
        if (target != null && !target.IsDead)
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                target,
                DynamicVars.Damage.BaseValue,
                ValueProp.Move,
                Owner,
                null);
        }

        if (Amount <= 1)
            await PowerCmd.Remove(this);
        else
            await PowerCmd.Decrement(this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner) return;
        if (result.UnblockedDamage <= 0) return;
        await PowerCmd.Remove(this);
    }

    private Creature? ResolveTarget()
    {
        int targetIndex = (int)DynamicVars[TargetIndexKey].BaseValue - 1;
        var combatState = Owner.CombatState;
        if (combatState == null) return null;
        var enemies = combatState.Enemies;
        if (targetIndex < 0 || targetIndex >= enemies.Count) return null;
        return enemies[targetIndex];
    }
}
