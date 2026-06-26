using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
public static class DeniaRestSitePatch
{
    private const string PortraitPath = "res://images/packed/character_select/denia_pink.png";

    [HarmonyPostfix]
    private static void Postfix(NRestSiteRoom __instance)
    {
        var tex = ResourceLoader.Load<Texture2D>(PortraitPath);
        if (tex == null) return;

        for (int i = 0; i < __instance.Characters.Count; i++)
        {
            var ch = __instance.Characters[i];
            if (ch.Player.Character is not Denia) continue;

            if (ch.GetNodeOrNull<Sprite2D>("DeniaRestSprite") != null) continue;

            foreach (var child in ch.GetChildren())
                if (child is Node2D n2d && n2d.GetClass() == "SpineSprite")
                    n2d.Visible = false;

            float scale = 320f / tex.GetWidth();
            var sprite = new Sprite2D
            {
                Name = "DeniaRestSprite",
                Texture = tex,
                Centered = true,
                Scale = new Vector2(scale, scale),
                FlipH = i % 2 == 1
            };
            ch.AddChild(sprite);
        }
    }
}
