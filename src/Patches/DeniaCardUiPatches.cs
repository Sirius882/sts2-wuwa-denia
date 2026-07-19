using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Denia;

/// <summary>卡面 Portrait 拉伸填充。</summary>
[HarmonyPatch(typeof(NCard), "_Ready")]
public static class DeniaCardPortraitFillPatch
{
    public static void Postfix(NCard __instance)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(__instance)) return;
            var portrait = __instance.GetNodeOrNull<TextureRect>("%Portrait");
            if (portrait != null && GodotObject.IsInstanceValid(portrait))
            {
                portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                portrait.StretchMode = TextureRect.StretchModeEnum.Scale;
            }
        }
        catch { }
    }
}

/// <summary>达妮娅能量图标改为纯文字，避免 BBCode img 撑破行高。</summary>
[HarmonyPatch(
    typeof(MegaCrit.Sts2.Core.Localization.Formatters.EnergyIconsFormatter),
    nameof(MegaCrit.Sts2.Core.Localization.Formatters.EnergyIconsFormatter.TryEvaluateFormat))]
public static class DeniaEnergyIconTextPatch
{
    public static bool Prefix(SmartFormat.Core.Extensions.IFormattingInfo formattingInfo, ref bool __result)
    {
        string? prefix = null;
        if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.EnergyVar ev)
            prefix = ev.ColorPrefix;

        if (string.IsNullOrEmpty(prefix))
            prefix = formattingInfo.CurrentValue as string;

        if (string.IsNullOrEmpty(prefix) || prefix == "colorless")
            prefix = MegaCrit.Sts2.Core.Runs.RunManager.Instance.GetLocalCharacterEnergyIconPrefix();

        if (prefix != "denia")
            return true;

        int count = 1;
        if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.EnergyVar ev2)
            count = Convert.ToInt32(ev2.PreviewValue);
        else if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar cv)
            count = Convert.ToInt32(cv.Calculate(null));
        else if (formattingInfo.CurrentValue is int i)
            count = i;
        else if (formattingInfo.CurrentValue is decimal d)
            count = (int)d;
        else if (formattingInfo.CurrentValue is string && int.TryParse(formattingInfo.FormatterOptions, out int parsed))
            count = parsed;

        string text = count switch { 1 => "能量", 2 => "2能量", 3 => "3能量", _ => $"{count}能量" };
        formattingInfo.Write(text);
        __result = true;
        return false;
    }
}
