// 达妮娅能量UI补丁：
// 1. 劫持 EnergyIconHelper.GetPath 将 "denia" 前缀映射到自定 PNG（卡牌能量图标）
// 2. 在 NCombatUi.Activate 时替换 NEnergyCounter 能量球贴图（战斗栏能量球）
#nullable enable

using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

[HarmonyPatch(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPath), typeof(string))]
public static class DeniaEnergyIconPathPatch
{
    private const string DeniaEnergyIconResourcePath =
        "res://images/packed/sprite_fonts/denia_energy_icon.png";

    private static bool Prefix(string prefix, ref string __result)
    {
        if (string.Equals(prefix, "denia", System.StringComparison.OrdinalIgnoreCase))
        {
            __result = DeniaEnergyIconResourcePath;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NCombatUi), "Activate")]
public static class DeniaEnergyCounterOrbPatch
{
    private static readonly AccessTools.FieldRef<NCombatUi, NEnergyCounter> EnergyCounterRef =
        AccessTools.FieldRefAccess<NCombatUi, NEnergyCounter>("_energyCounter");

    private static Texture2D? _cachedIcon;

    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        Player? me = state.Players.FirstOrDefault();
        if (me?.Character is not Denia) return;

        var energyCounter = EnergyCounterRef(__instance);
        if (energyCounter == null) return;

        var tex = LoadOrCacheIcon();
        if (tex == null) return;

        SwapAllTextures(energyCounter.GetNodeOrNull("%Layers"), tex);
        SwapAllTextures(energyCounter.GetNodeOrNull("%RotationLayers"), tex);
    }

    private static void SwapAllTextures(Node? parent, Texture2D tex)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;
        foreach (var child in parent.GetChildren())
        {
            if (child is TextureRect tr && GodotObject.IsInstanceValid(tr))
            {
                try { tr.Texture = tex; }
                catch { }
            }
        }
    }

    private static Texture2D? LoadOrCacheIcon()
    {
        if (_cachedIcon != null && GodotObject.IsInstanceValid(_cachedIcon))
            return _cachedIcon;

        const string path = "res://images/packed/sprite_fonts/denia_energy_icon.png";
        try
        {
            _cachedIcon = ResourceLoader.Load<Texture2D>(path);
            if (_cachedIcon == null || _cachedIcon.GetWidth() <= 0)
            {
                var img = Image.LoadFromFile(path);
                if (img.GetWidth() > 0 && img.GetHeight() > 0)
                    _cachedIcon = ImageTexture.CreateFromImage(img);
            }
        }
        catch { }
        return _cachedIcon;
    }
}
