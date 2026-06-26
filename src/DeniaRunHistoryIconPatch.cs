#nullable enable

using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;

namespace Denia;

/// <summary>
/// 修复战绩记录页面达妮娅头像过大问题：
/// 给 Icon TextureRect 补上 expand_mode 和 stretch_mode，
/// 让任意尺寸的贴图都能自适应 64×64 的框架容器。
/// </summary>
[HarmonyPatch(typeof(NRunHistoryPlayerIcon), "_Ready")]
public static class DeniaRunHistoryIconPatch
{
    public static void Postfix(NRunHistoryPlayerIcon __instance)
    {
        var icon = __instance.GetNode<TextureRect>("%Icon");
        icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }
}
