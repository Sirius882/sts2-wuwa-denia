using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
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
