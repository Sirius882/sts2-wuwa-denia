using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// BaseLib ModSourceTooltip.Fold：tips 为空时 list[0]/list[Count-1] 越界。
/// 隐藏 power 的 HoverTips 为空，Creature 悬停聚合时触发。
/// Prefix 在空列表时跳过 Fold，原样返回 tips。
/// </summary>
[HarmonyPatch]
public static class DeniaModSourceTooltipEmptyGuardPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("BaseLib.Patches.UI.ModSourceTooltip");
        if (type == null)
            throw new System.InvalidOperationException("[Denia] BaseLib.Patches.UI.ModSourceTooltip not found");
        // private static IEnumerable<IHoverTip> Fold(IEnumerable<IHoverTip> tips, AbstractModel model, bool foldLast = false)
        return AccessTools.Method(type, "Fold", new[]
        {
            typeof(IEnumerable<IHoverTip>),
            typeof(AbstractModel),
            typeof(bool)
        }) ?? AccessTools.Method(type, "Fold");
    }

    public static bool Prefix(IEnumerable<IHoverTip> tips, ref IEnumerable<IHoverTip> __result)
    {
        if (tips == null)
        {
            __result = System.Array.Empty<IHoverTip>();
            return false;
        }

        if (tips is ICollection<IHoverTip> col)
        {
            if (col.Count == 0)
            {
                __result = tips;
                return false;
            }
            return true;
        }

        // 避免对非集合重复枚举：先物化空检测
        var list = tips as IList<IHoverTip> ?? tips.ToList();
        if (list.Count == 0)
        {
            __result = list;
            return false;
        }

        // 非空且已物化：不能简单 return true 否则 Fold 收到原 tips 可能再 ToList；直接 true 用原 tips 即可
        return true;
    }
}
