using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// BaseLib ModSourceTooltip.Fold：tips 为空时 list[0] 越界。
/// TargetMethod 找不到类型时返回 null 并跳过（勿 throw，否则 PatchAll 启动失败）。
/// </summary>
[HarmonyPatch]
public static class DeniaModSourceTooltipEmptyGuardPatch
{
    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
    {
        try
        {
            var type = AccessTools.TypeByName("BaseLib.Patches.UI.ModSourceTooltip");
            if (type == null)
            {
                GD.Print("[Denia] ModSourceTooltip type not found; Fold empty-guard skipped.");
                return null;
            }

            var method = AccessTools.Method(type, "Fold", new[]
            {
                typeof(IEnumerable<IHoverTip>),
                typeof(AbstractModel),
                typeof(bool)
            }) ?? AccessTools.Method(type, "Fold");

            if (method == null)
                GD.Print("[Denia] ModSourceTooltip.Fold not found; empty-guard skipped.");
            return method;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] ModSourceTooltip empty-guard TargetMethod failed: {ex.Message}");
            return null;
        }
    }

    public static bool Prefix(IEnumerable<IHoverTip> tips, ref IEnumerable<IHoverTip> __result)
    {
        if (tips == null)
        {
            __result = Array.Empty<IHoverTip>();
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

        var list = tips as IList<IHoverTip> ?? tips.ToList();
        if (list.Count == 0)
        {
            __result = list;
            return false;
        }
        return true;
    }
}
