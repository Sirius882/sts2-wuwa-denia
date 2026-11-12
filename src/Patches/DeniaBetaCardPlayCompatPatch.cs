#nullable enable
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Denia;

/// <summary>
/// 测试版 CardModel.OnPlayWrapper 在结算跨越战斗边界时，会按 CombatId 门控跳过结果移堆。
/// 本补丁不改变该门控，也不重放任何结果位置或卡牌结算；它只在旧战斗已经失效后，
/// 回收仍可从当前桌面 UI 找到的 Denia 卡牌节点，避免视觉上悬留在屏幕中央。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
public static class DeniaBetaCardPlayCompatPatch
{
    /// <summary>
    /// 只记录 Denia 卡牌进入 OnPlayWrapper 时所属的战斗。非 Denia 卡牌的 state 保持为空，
    /// 从而完全绕过兼容逻辑。
    /// </summary>
    private static void Prefix(CardModel __instance, out CombatId? __state)
    {
        __state = __instance is DeniaCard
            ? CombatManager.Instance.CurrentCombatId
            : null;
    }

    /// <summary>
    /// 保持原 OnPlayWrapper 的所有异常和完成时机。原任务返回后才判断旧战斗是否失效；
    /// 因此不会处理 BranchingPlayerChoiceContext 等仍在等待的任务。
    /// </summary>
    private static void Postfix(CardModel __instance, CombatId? __state, ref Task __result)
    {
        if (__instance is not DeniaCard || __state == null)
            return;

        __result = AwaitOriginalAndRecycleVisualAsync(__result, __instance, __state);
    }

    private static async Task AwaitOriginalAndRecycleVisualAsync(Task originalTask, CardModel card, CombatId? effectCombatId)
    {
        // 不 catch：原卡牌结算异常必须原样传播，不能被兼容层吞掉。
        await originalTask;

        // 只清理已经结束或已切换的旧战斗。live combat 中绝不触碰卡牌节点或模型。
        if (CombatManager.Instance.IsCurrentLiveCombat(effectCombatId))
            return;

        // FindOnTable 是 beta 公开的安全 UI 查询。若旧房间已离开或游戏已不在战斗中，
        // 它会返回 null；此时不访问内部牌堆集合，旧战斗模型会由房间销毁流程统一回收。
        NCard? cardNode = NCard.FindOnTable(card);
        cardNode?.QueueFreeSafely();

        // 不调用 RemoveFromCurrentPile / CardPileCmd：这些操作可能将旧模型接入当前战斗，
        // 或再次触发牌堆、Exhaust、Played 等 hook。这里只做上述安全视觉回收。
    }
}
