using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace Denia;

[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
public static class DeniaMerchantPatch
{
    private const string PortraitPath = "res://images/packed/character_select/denia_pink.png";

    private static readonly AccessTools.FieldRef<NMerchantRoom, List<Player>> PlayersRef =
        AccessTools.FieldRefAccess<NMerchantRoom, List<Player>>("_players");

    [HarmonyPostfix]
    private static void Postfix(NMerchantRoom __instance)
    {
        var tex = ResourceLoader.Load<Texture2D>(PortraitPath);
        if (tex == null) return;

        var players = PlayersRef(__instance);
        var visuals = __instance.PlayerVisuals;
        int count = Mathf.Min(players.Count, visuals.Count);

        for (int i = 0; i < count; i++)
        {
            if (players[i].Character is not Denia) continue;
            var container = visuals[i];
            if (container.GetNodeOrNull<Sprite2D>("DeniaMerchSprite") != null) continue;

            // 隐藏原版 Spine 模型
            foreach (var child in container.GetChildren())
                if (child is Node2D n2d && n2d.GetClass() == "SpineSprite")
                    n2d.Visible = false;

            // 缩放匹配容器 — 以 447x700 为基准
            float scale = 320f / tex.GetWidth();
            var sprite = new Sprite2D
            {
                Name = "DeniaMerchSprite",
                Texture = tex,
                Centered = true,
                Position = new Vector2(0, -50f),
                Scale = new Vector2(scale, scale)
            };
            container.AddChild(sprite);
        }
    }
}

[HarmonyPatch(typeof(NFakeMerchant), nameof(NFakeMerchant._Ready))]
public static class DeniaFakeMerchantPatch
{
    private const string PortraitPath = "res://images/packed/character_select/denia_pink.png";

    private static readonly AccessTools.FieldRef<NFakeMerchant, List<Player>> PlayersRef =
        AccessTools.FieldRefAccess<NFakeMerchant, List<Player>>("_players");

    [HarmonyPostfix]
    private static void Postfix(NFakeMerchant __instance)
    {
        var tex = ResourceLoader.Load<Texture2D>(PortraitPath);
        if (tex == null) return;

        var characterContainer = __instance.GetNodeOrNull<Control>("%CharacterContainer");
        if (characterContainer == null || !GodotObject.IsInstanceValid(characterContainer)) return;

        var players = PlayersRef(__instance);
        var visuals = characterContainer.GetChildren().OfType<NCreatureVisuals>().ToList();
        int count = Mathf.Min(players.Count, visuals.Count);
        for (int i = 0; i < count; i++)
        {
            if (players[i].Character is not Denia) continue;
            ReplacePlaceholderVisual(visuals[count - 1 - i], tex);
        }
    }

    private static void ReplacePlaceholderVisual(NCreatureVisuals visuals, Texture2D tex)
    {
        if (visuals.GetNodeOrNull<Sprite2D>("DeniaFakeMerchantSprite") != null) return;

        var body = visuals.GetNodeOrNull<Node2D>("%Visuals");
        if (body != null && GodotObject.IsInstanceValid(body))
            body.Visible = false;

        var bounds = visuals.GetNodeOrNull<Control>("%Bounds");
        if (bounds != null && GodotObject.IsInstanceValid(bounds))
        {
            bounds.OffsetLeft = -105f;
            bounds.OffsetTop = -270f;
            bounds.OffsetRight = 105f;
            bounds.OffsetBottom = 0f;
        }

        float scale = Mathf.Min(210f / tex.GetWidth(), 270f / tex.GetHeight());
        var sprite = new Sprite2D
        {
            Name = "DeniaFakeMerchantSprite",
            Texture = tex,
            Centered = true,
            Position = new Vector2(0f, -135f),
            Scale = new Vector2(scale, scale)
        };
        visuals.AddChild(sprite);
        visuals.MoveChild(sprite, 0);
    }
}
