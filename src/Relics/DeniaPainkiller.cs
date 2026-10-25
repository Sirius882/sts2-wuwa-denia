using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

/// <summary>
/// 止痛药（虚血）：敌方回合内你失去的生命值会被记入遗物计数；
/// 你的回合内你对怪物造成伤害时，回复该次伤害 2/3 的生命值，
/// 本回合回复总量不超过记录值；你的回合结束时清零记录。
/// 记录值实时显示在遗物图标右下角（剩余可用回复量）。
/// 全部走遗物生命周期钩子，仅对持有者结算，不影响联机队友。
/// </summary>
[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaPainkiller : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override string IconBaseName => "denia_painkiller";

    /// <summary>敌方回合累计记录的掉血量（每场战斗重置）。</summary>
    internal decimal RecordedLoss;

    /// <summary>本回合已通过虚血回复的生命值（受 RecordedLoss 上限约束）。</summary>
    internal decimal HealedThisTurn;

    /// <summary>当前是否处于持有者自己的回合（仅此窗口内造成伤害才回血）。</summary>
    internal bool InOwnerTurn;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress && !IsCanonical;

    public override int DisplayAmount
    {
        get
        {
            if (!CombatManager.Instance.IsInProgress || IsCanonical) return -1;
            decimal remaining = RecordedLoss - HealedThisTurn;
            return remaining <= 0m ? 0 : (int)System.Math.Floor(remaining);
        }
    }

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "止痛药",
            Description: "省流：虚血\n每个敌怪的回合，记录该回合你失去的生命值；在下个你的回合，当你对怪物造成伤害时，回复伤害量2/3的生命值。每回合通过这种方式回复的生命值总量不超过记录的上回合掉血量。在你的每个回合结束时重置记录。",
            Flavor: "被残星会会长换成了生理盐水，并没有止痛的效果。");

    public override Task AfterObtained()
    {
        CombatManager.Instance.CombatSetUp -= OnCombatSetUp;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        // 保底：起始遗物不会触发 AfterObtained，用 AfterRoomEntered 降级兜底
        CombatManager.Instance.CombatSetUp -= OnCombatSetUp;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        return Task.CompletedTask;
    }

    private void OnCombatSetUp(ICombatState combatState)
    {
        RecordedLoss = 0m;
        HealedThisTurn = 0m;
        InOwnerTurn = false;
        RefreshDisplay();
    }

    /// <summary>供外部刷新遗物 UI 计数。</summary>
    public void RefreshDisplay() => InvokeDisplayAmountChanged();

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner?.Creature == null) return;

        if (side == CombatSide.Player)
        {
            if (!participants.Contains(Owner.Creature)) return;
            InOwnerTurn = true;
        }
        else
        {
            // 敌方回合开始：进入掉血记录窗口
            InOwnerTurn = false;
        }

        await Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        if (Owner?.Creature == null) return;

        // 你的回合结束：重置记录，退出回血窗口
        InOwnerTurn = false;
        RecordedLoss = 0m;
        HealedThisTurn = 0m;
        RefreshDisplay();
        await Task.CompletedTask;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (Owner?.Creature == null || creature != Owner.Creature) return;

        // 只记录敌方回合内（非你回合窗口）的生命值损失
        if (delta >= 0m || InOwnerTurn) return;

        RecordedLoss += -delta;
        Flash();
        RefreshDisplay();
        await Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        MegaCrit.Sts2.Core.ValueProps.ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Owner?.Creature == null || dealer != Owner.Creature) return;
        if (target.IsPlayer || !InOwnerTurn) return;
        if (Owner.Creature.IsDead) return;

        decimal budget = RecordedLoss - HealedThisTurn;
        if (budget <= 0m) return;

        // 实际扣血 = 穿过格挡的伤害扣除溢出击杀部分
        int dealt = result.UnblockedDamage - result.OverkillDamage;
        if (dealt <= 0) return;

        // 回血量按 2/3 向上取整（用户口径）；引擎的回血浮字（NHealNumVfx 直接 (int) 强转）
        // 与血条显示都按整数截断，小数回血会显示"+0"，因此必须取整后再回血。
        // 预算上限保持严格：剩余额度向下取整，总量永远不超过记录的掉血量；不足 1 点则不回血、不消耗额度。
        decimal heal = System.Math.Min(
            System.Math.Ceiling(dealt * 2m / 3m),
            System.Math.Floor(budget));
        if (heal < 1m) return;

        HealedThisTurn += heal;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, heal);
        RefreshDisplay();
    }
}
