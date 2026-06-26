// 达妮娅卡牌左上角虚质/黯核资源图标补丁。参照 AemeathCardCostIconPatch 实现。
#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Denia;

/// <summary>
/// 在 AemeathCardCostIconPatch 之后执行，确保达妮娅卡牌的虚质/黯核图标不被 Aemeath 补丁覆盖。
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateVisuals")]
[HarmonyAfter("sts2.aemeath.ww")]
public static class DeniaCardCostIconPatch
{
    private const string VirtualMatterIconPath =
        "res://images/ui/combat/denia_virtual_matter_cost_icon.png";

    private const string DarkCoreIconPath =
        "res://images/ui/combat/denia_dark_core_cost_icon.png";

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private static readonly Dictionary<ulong, Texture2D?> DefaultStarIconTextures = new();

    private static void Postfix(NCard __instance)
    {
        try
        {
            PostfixImpl(__instance);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] CardCostIcon Postfix error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PostfixImpl(NCard __instance)
    {
        if (!GodotObject.IsInstanceValid(__instance)) return;

        TextureRect? starIcon = __instance.GetNodeOrNull<TextureRect>("%StarIcon");
        TextureRect? energyIcon = __instance.GetNodeOrNull<TextureRect>("%EnergyIcon");
        Label? starLabel = __instance.GetNodeOrNull<Label>("%StarLabel");

        CaptureDefaultTexture(starIcon, DefaultStarIconTextures);

        if (__instance.Model is not DeniaCard card)
            return;

        int vmCost = card.CurrentVirtualMatterCost;
        int dcCost = card.CurrentDarkCoreCost;

        if (energyIcon != null && GodotObject.IsInstanceValid(energyIcon))
            energyIcon.Visible = true;

        if (starIcon == null || !GodotObject.IsInstanceValid(starIcon)) return;

        if (vmCost > 0)
        {
            ApplyTexture(starIcon, VirtualMatterIconPath);
            SafeSetLabel(starLabel, vmCost.ToString());
            SetVisible(starIcon, true);
        }
        else if (dcCost > 0)
        {
            ApplyTexture(starIcon, DarkCoreIconPath);
            SafeSetLabel(starLabel, dcCost.ToString());
            SetVisible(starIcon, true);
        }
        else
        {
            SetVisible(starIcon, false);
            SafeSetLabel(starLabel, string.Empty);
        }
    }

    private static void SetVisible(TextureRect? node, bool visible)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        try { node.Visible = visible; }
        catch (Exception ex) { GD.PrintErr($"[Denia] CardCostIcon SetVisible: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void SafeSetLabel(Label? node, string text)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        try { node.Text = text; }
        catch (Exception ex) { GD.PrintErr($"[Denia] CardCostIcon SetLabel: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void CaptureDefaultTexture(TextureRect? node, Dictionary<ulong, Texture2D?> cache)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        try
        {
            ulong key = node.GetInstanceId();
            if (!cache.ContainsKey(key))
            {
                Texture2D? texture = node.Texture;
                cache[key] = IsTextureValid(texture) ? texture : null;
            }
        }
        catch (Exception ex) { GD.PrintErr($"[Denia] CardCostIcon CaptureTexture: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void ApplyTexture(TextureRect? node, string path)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;

        if (TextureCache.TryGetValue(path, out Texture2D? cached) && IsTextureValid(cached))
        {
            node.Texture = cached;
            return;
        }

        TextureCache.Remove(path);

        try
        {
            Texture2D? texture = ResourceLoader.Load<Texture2D>(path);

            if (!IsTextureValid(texture))
            {
                Image image = Image.LoadFromFile(path);
                if (image.GetWidth() > 0 && image.GetHeight() > 0)
                    texture = ImageTexture.CreateFromImage(image);
            }

            if (IsTextureValid(texture))
            {
                TextureCache[path] = texture;
                node.Texture = texture;
            }
        }
        catch (Exception ex) { GD.PrintErr($"[Denia] CardCostIcon ApplyTexture({path}): {ex.GetType().Name}: {ex.Message}"); }
    }

    private static bool IsTextureValid(Texture2D? texture)
    {
        return texture != null && GodotObject.IsInstanceValid(texture) && texture.GetWidth() > 0;
    }
}
