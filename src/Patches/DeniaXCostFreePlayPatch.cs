#nullable enable
using System;
using System.Collections;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 对齐原版 X 费「免费打出」语义（以 CardCmd.AutoPlay 为准）：
/// - X = 当前剩余能量（CapturedXValue）
/// - 实际不扣能量（EnergySpent = 0）
/// 手动路径中 GetAmountToSpend 对 CostsX 恒返回当前能量，忽略 SetToFree / FreeAttack 等，
/// 导致「免费」仍扣光能量。本补丁仅在能量免费时接管 SpendResources。
/// 牌面费用仍显示 X（与原版 NCard.UpdateEnergyCostVisuals 一致，不改显示）。
/// </summary>
public static class DeniaXCostFreePlay
{
    private static readonly AccessTools.FieldRef<CardEnergyCost, object?>? LocalModifiersRef =
        CreateLocalModifiersRef();

    private static AccessTools.FieldRef<CardEnergyCost, object?>? CreateLocalModifiersRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<CardEnergyCost, object?>("_localModifiers");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>X 费牌当前是否应按「免费打出」结算（不扣能量、X 取当前能量）。</summary>
    public static bool IsEnergyFreeXCost(CardModel card)
    {
        if (card == null || !card.EnergyCost.CostsX)
            return false;

        if (HasAbsoluteZeroLocalEnergyCost(card.EnergyCost))
            return true;

        if (card.CombatState == null)
            return false;

        // GetWithModifiers 对 CostsX 会提前 return，全局免费 Hook（FreeAttack 等）进不去。
        // 用假的非零底价跑 ModifyEnergyCostInCombat。
        decimal modified = Hook.ModifyEnergyCostInCombat(card.CombatState, card, 1m);
        return modified <= 0m;
    }

    private static bool HasAbsoluteZeroLocalEnergyCost(CardEnergyCost energyCost)
    {
        if (!energyCost.HasLocalModifiers || LocalModifiersRef == null)
            return false;

        object? listObj;
        try { listObj = LocalModifiersRef(energyCost); }
        catch { return false; }

        if (listObj is not IEnumerable modifiers)
            return false;

        foreach (object? mod in modifiers)
        {
            if (mod == null) continue;
            var typeProp = AccessTools.Property(mod.GetType(), "Type");
            var amountProp = AccessTools.Property(mod.GetType(), "Amount");
            if (typeProp == null || amountProp == null) continue;

            // LocalCostType: None=0, Absolute=1, Relative=2
            if (Convert.ToInt32(typeProp.GetValue(mod)) == (int)LocalCostType.Absolute
                && Convert.ToInt32(amountProp.GetValue(mod)) == 0)
                return true;
        }

        return false;
    }
}

/// <summary>
/// 手动打出 X 费且能量免费时：捕获当前能量为 X，能量实扣 0；星费仍按原逻辑。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
public static class DeniaXCostFreePlaySpendPatch
{
    private static bool Prefix(CardModel __instance, ref Task<(int, int)> __result)
    {
        if (!DeniaXCostFreePlay.IsEnergyFreeXCost(__instance))
            return true;

        __result = SpendFreeXAsync(__instance);
        return false;
    }

    private static async Task<(int, int)> SpendFreeXAsync(CardModel card)
    {
        int energy = card.Owner.PlayerCombatState?.Energy ?? 0;
        // 与 AutoPlay 一致：X = 当前能量；EnergySpent = 0
        card.EnergyCost.CapturedXValue = energy;

        int starsToSpend = Math.Max(0, card.GetStarCostWithModifiers());
        card.LastStarsSpent = starsToSpend;
        if (starsToSpend > 0 && card.Owner.PlayerCombatState != null)
        {
            card.Owner.PlayerCombatState.LoseStars(starsToSpend);
            if (card.Owner.Creature.CombatState != null)
                await Hook.AfterStarsSpent(card.Owner.Creature.CombatState, starsToSpend, card.Owner);
        }

        if (card.CombatState != null)
            await Hook.AfterEnergySpent(card.CombatState, card, 0);

        return (0, starsToSpend);
    }
}
