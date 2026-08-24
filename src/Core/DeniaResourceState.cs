using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// Denia 双能量系统：虚质(VirtualMatter)与黯核(DarkCore)的读取与增删。
/// 消耗规则：只在[gold]黑色形态[/gold]消耗并强化卡牌；[gold]粉色形态[/gold]不消耗、不强化。
/// </summary>
public static class DeniaResourceState
{
    public const int DarkCoreMax = 5;
    public const int VirtualMatterMax = 20;

    private static readonly PlayerChoiceContext _throwing = new ThrowingPlayerChoiceContext();

    // ==================== 虚质 (VirtualMatter) ====================

    /// <summary>读取当前虚质层数。</summary>
    public static int GetVirtualMatter(Creature creature) =>
        creature.GetPower<DeniaVirtualMatterPower>()?.Amount ?? 0;

    /// <summary>增加虚质（上限 VirtualMatterMax = 20）。</summary>
    public static async Task GainVirtualMatter(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount <= 0) return;
        if (creature.GetPower<DeniaVirtualMatterLockedPower>() != null) return;
        int current = GetVirtualMatter(creature);
        int actual = Math.Min(amount, VirtualMatterMax - current);
        if (actual <= 0) return;
        await PowerCmd.Apply<DeniaVirtualMatterPower>(_throwing, creature, actual, applier, source);
    }

    /// <summary>
    /// 消耗虚质。只在[gold]黑色形态[/gold]生效，[gold]粉色形态[/gold]直接返回 false（未消耗，卡牌不应获得强化）。
    /// </summary>
    public static async Task<bool> TrySpendVirtualMatter(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount <= 0) return true;
        if (!DeniaFormHelper.IsBlack(creature)) return false;

        if (creature.GetPower<DeniaVirtualMatterLockedPower>() != null)
            return GetVirtualMatter(creature) >= amount;

        var power = creature.GetPower<DeniaVirtualMatterPower>();
        if (power == null || power.Amount < amount) return false;

        await PowerCmd.ModifyAmount(_throwing, power, -amount, applier, source);
        return true;
    }

    /// <summary>虚质归零（完全移除 Power）。</summary>
    public static async Task ClearVirtualMatter(
        Creature creature,
        Creature applier,
        CardModel source,
        bool ignoreLock = false)
    {
        _ = applier;
        _ = source;
        if (!ignoreLock && creature.GetPower<DeniaVirtualMatterLockedPower>() != null) return;
        await PowerCmd.Remove<DeniaVirtualMatterPower>(creature);
    }

    /// <summary>清空黯核；资源锁定期间普通清空不生效。</summary>
    public static async Task ClearDarkCore(
        Creature creature,
        Creature applier,
        CardModel source,
        bool ignoreLock = false)
    {
        if (!ignoreLock && creature.GetPower<DeniaDarkCoreLockedPower>() != null) return;
        await SetDarkCore(creature, 0, applier, source, ignoreLock: true);
    }

    // ==================== 黯核 (DarkCore) ====================
    // 黯核直接使用原生 Stars 作为真值（PlayerCombatState.Stars），
    // DeniaDarkCorePower 仅保留为兼容兆底，确保 NStarCounter 能正确显示 0~5 颗星。

    /// <summary>读取当前黯核数量。优先读原生 Stars，回退到 Power。</summary>
    public static int GetDarkCore(Creature creature)
    {
        var stars = creature.Player?.PlayerCombatState?.Stars;
        if (stars != null)
            return Math.Clamp(stars.Value, 0, DarkCoreMax);
        return creature.GetPower<DeniaDarkCorePower>()?.Amount ?? 0;
    }

    /// <summary>增加黯核（上限 DarkCoreMax = 5）。同步原生 Stars 与 Power 兜底。</summary>
    public static async Task GainDarkCore(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount <= 0) return;
        if (creature.GetPower<DeniaDarkCoreLockedPower>() != null) return;
        int current = GetDarkCore(creature);
        int target = Math.Clamp(current + amount, 0, DarkCoreMax);
        int delta = target - current;
        if (delta <= 0) return;

        if (creature.Player?.PlayerCombatState != null)
            await PlayerCmd.SetStars(target, creature.Player);

        // Power 兜底同步
        var power = creature.GetPower<DeniaDarkCorePower>();
        if (power != null)
            await PowerCmd.ModifyAmount(_throwing, power, delta, applier, source);
        else
            await PowerCmd.Apply<DeniaDarkCorePower>(_throwing, creature, target, applier, source);
    }

    /// <summary>
    /// 消耗黯核。只在[gold]黑色形态[/gold]生效，[gold]粉色形态[/gold]直接返回 false（未消耗，卡牌不应获得强化）。
    /// 同步原生 Stars 与 Power 兜底。
    /// </summary>
    public static async Task<bool> TrySpendDarkCore(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount <= 0) return true;
        if (!DeniaFormHelper.IsBlack(creature)) return false;

        int current = GetDarkCore(creature);
        if (current < amount) return false;
        if (creature.GetPower<DeniaDarkCoreLockedPower>() != null) return true;

        int newVal = current - amount;
        if (creature.Player?.PlayerCombatState != null)
            await PlayerCmd.SetStars(newVal, creature.Player);

        // Power 兜底同步
        var power = creature.GetPower<DeniaDarkCorePower>();
        if (power != null)
            await PowerCmd.ModifyAmount(_throwing, power, -amount, applier, source);

        return true;
    }

    /// <summary>将虚质直接设置为指定值，用于事件卡资源锁定。</summary>
    public static async Task SetVirtualMatter(
        Creature creature,
        int amount,
        Creature applier,
        CardModel source,
        bool ignoreLock = false)
    {
        if (!ignoreLock && creature.GetPower<DeniaVirtualMatterLockedPower>() != null) return;
        amount = Math.Clamp(amount, 0, VirtualMatterMax);
        var power = creature.GetPower<DeniaVirtualMatterPower>();
        if (amount == 0)
        {
            if (power != null)
                await PowerCmd.Remove<DeniaVirtualMatterPower>(creature);
            return;
        }

        if (power == null)
        {
            await PowerCmd.Apply<DeniaVirtualMatterPower>(_throwing, creature, amount, applier, source);
            return;
        }

        int delta = amount - (int)power.Amount;
        if (delta != 0)
            await PowerCmd.ModifyAmount(_throwing, power, delta, applier, source);
    }

    /// <summary>将黯核直接设置为指定值，并同步原生 Stars 与兜底 Power。</summary>
    public static async Task SetDarkCore(
        Creature creature,
        int amount,
        Creature applier,
        CardModel source,
        bool ignoreLock = false)
    {
        if (!ignoreLock && creature.GetPower<DeniaDarkCoreLockedPower>() != null) return;
        amount = Math.Clamp(amount, 0, DarkCoreMax);
        if (creature.Player?.PlayerCombatState != null)
            await PlayerCmd.SetStars(amount, creature.Player);

        var power = creature.GetPower<DeniaDarkCorePower>();
        if (amount == 0)
        {
            if (power != null)
                await PowerCmd.Remove<DeniaDarkCorePower>(creature);
            return;
        }

        if (power == null)
        {
            await PowerCmd.Apply<DeniaDarkCorePower>(_throwing, creature, amount, applier, source);
            return;
        }

        int delta = amount - (int)power.Amount;
        if (delta != 0)
            await PowerCmd.ModifyAmount(_throwing, power, delta, applier, source);
    }
}
