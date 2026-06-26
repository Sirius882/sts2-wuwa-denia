using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

[HarmonyPatch]
public static class DeniaCardRewardRarityFallbackPatch
{
    /// <summary>
    /// 稀有度回退优先级：Rare > Uncommon > Common > Basic > 其他。
    /// 当 RollForRarity 无法匹配到有效稀有度时，按此顺序选择最接近的可用稀有度。
    /// </summary>
    private static readonly CardRarity[] FallbackPriority =
    {
        CardRarity.Rare,
        CardRarity.Uncommon,
        CardRarity.Common,
        CardRarity.Basic
    };

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CardFactory),
            "RollForRarity",
            new[] {
                typeof(Player),
                typeof(CardRarityOddsType),
                typeof(CardCreationSource),
                typeof(HashSet<CardRarity>),
                typeof(bool)
            });
    }

    /// <summary>
    /// 当 RollForRarity 无法匹配到有效稀有度（返回 CardRarity.None）时，
    /// 从 allowedRarities 中按优先级选择回退稀有度，避免 InvalidOperationException 崩溃。
    /// </summary>
    public static void Postfix(ref CardRarity __result, HashSet<CardRarity> allowedRarities)
    {
        if (__result == CardRarity.None && allowedRarities != null && allowedRarities.Count > 0)
        {
            __result = allowedRarities
                .OrderByDescending(r => System.Array.IndexOf(FallbackPriority, r))
                .First();
        }
    }
}
