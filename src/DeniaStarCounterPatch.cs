// 文件说明：实现达妮娅黯核计数器图标与旋转层补丁，替换原生星辉图标为黯核图标。
#nullable enable

using System;
using System.Collections.Generic;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

[HarmonyPatch(typeof(NStarCounter), nameof(NStarCounter.Initialize))]
public static class DeniaStarCounterPatch
{
    private const string IconTexturePath =
        "res://images/ui/combat/denia_dark_core_icon.png";

    // 黯核只有一个图标资源，旋转层复用同一张图标。
    private static readonly string[] RotationLayerTexturePaths =
    [
        "res://images/ui/combat/denia_dark_core_icon.png",
        "res://images/ui/combat/denia_dark_core_icon.png"
    ];

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private static readonly Dictionary<ulong, Texture2D?> DefaultRotationLayerTextures = new();
    private static readonly Dictionary<ulong, Texture2D?> DefaultIconTextures = new();

    private static void Postfix(NStarCounter __instance, Player player)
    {
        TextureRect? icon = __instance.GetNodeOrNull<TextureRect>("Icon");
        Control? rotationLayers = __instance.GetNodeOrNull<Control>("%RotationLayers");

        CaptureDefaultTexture(icon, DefaultIconTextures);
        CaptureRotationLayerTextures(rotationLayers);

        if (player.Character is not Denia)
        {
            // 星计数器节点也可能被复用，非达妮娅时要恢复默认星辉资源。
            RestoreDefaultTexture(icon, DefaultIconTextures);
            RestoreRotationLayerTextures(rotationLayers);
            if (rotationLayers != null) rotationLayers.Visible = true;
            __instance.Scale = Godot.Vector2.One;
            return;
        }

        ApplyIconTexture(icon);
        ApplyRotationLayerTextures(rotationLayers);

        // 黯核不像原版储君星能那样旋转，隐藏旋转层
        if (rotationLayers != null) rotationLayers.Visible = false;

        // 黯核UI缩小到2/3并稍微下移
        __instance.Scale = new Godot.Vector2(0.667f, 0.667f);
        __instance.Position = __instance.Position with { Y = __instance.Position.Y + 48f };
    }

    private static void ApplyIconTexture(TextureRect? icon)
    {
        if (icon == null)
            return;

        Texture2D? texture = LoadTexture(IconTexturePath);
        if (IsTextureAlive(texture))
            icon.Texture = texture;
    }

    private static void ApplyRotationLayerTextures(Control? root)
    {
        if (root == null)
            return;

        for (int i = 0; i < RotationLayerTexturePaths.Length; i++)
        {
            Texture2D? texture = LoadTexture(RotationLayerTexturePaths[i]);
            if (!IsTextureAlive(texture) || i >= root.GetChildCount())
                continue;

            if (root.GetChild(i) is TextureRect textureRect)
                textureRect.Texture = texture;
        }
    }

    private static void CaptureRotationLayerTextures(Control? root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.GetChildCount(); i++)
        {
            if (root.GetChild(i) is TextureRect textureRect)
            {
                CaptureDefaultTexture(textureRect, DefaultRotationLayerTextures);
            }
        }
    }

    private static void RestoreRotationLayerTextures(Control? root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.GetChildCount(); i++)
        {
            if (root.GetChild(i) is TextureRect textureRect)
            {
                RestoreDefaultTexture(textureRect, DefaultRotationLayerTextures);
            }
        }
    }

    private static void CaptureDefaultTexture(TextureRect? node, Dictionary<ulong, Texture2D?> cache)
    {
        if (node == null)
            return;

        ulong key = node.GetInstanceId();
        if (!cache.ContainsKey(key))
        {
            cache[key] = node.Texture;
        }
    }

    private static void RestoreDefaultTexture(TextureRect? node, Dictionary<ulong, Texture2D?> cache)
    {
        if (node == null)
            return;

        if (cache.TryGetValue(node.GetInstanceId(), out Texture2D? defaultTexture))
        {
            // Some Godot resources can be disposed after scene transitions.
            // Avoid assigning stale handles back to UI nodes.
            if (IsTextureAlive(defaultTexture))
            {
                node.Texture = defaultTexture;
            }
            else
            {
                node.Texture = null;
            }
        }
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (TextureCache.TryGetValue(path, out Texture2D? cached))
        {
            if (IsTextureAlive(cached))
                return cached;

            TextureCache.Remove(path);
        }

        Texture2D? texture = null;
        try
        {
            texture = ResourceLoader.Load<Texture2D>(path);
        }
        catch
        {
            texture = null;
        }

        if (texture == null)
        {
            try
            {
                Image image = Image.LoadFromFile(path);
                if (image.GetWidth() > 0 && image.GetHeight() > 0)
                    texture = ImageTexture.CreateFromImage(image);
            }
            catch
            {
                texture = null;
            }
        }

        TextureCache[path] = texture;
        return texture;
    }

    private static bool IsTextureAlive(Texture2D? texture)
    {
        return texture != null && GodotObject.IsInstanceValid(texture);
    }
}
