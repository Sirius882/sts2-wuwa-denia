using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

/// <summary>
/// 火堆立绘：占位 necro spine 隐藏后叠 denia_rest_site（artResources 火堆.png）。
/// 单人时原 scale=320/width 过小；按目标高度约 520 并上移锚点。
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
public static class DeniaRestSitePatch
{
    private const string PortraitPath = "res://images/packed/character_select/denia_rest_site.png";
    private const float TargetHeight = 520f;

    [HarmonyPostfix]
    private static void Postfix(NRestSiteRoom __instance)
    {
        Texture2D? tex;
        try { tex = ResourceLoader.Load<Texture2D>(PortraitPath); }
        catch { return; }
        if (tex == null) return;

        float h = tex.GetHeight();
        if (h <= 0f) return;
        float scale = TargetHeight / h;

        for (int i = 0; i < __instance.Characters.Count; i++)
        {
            var ch = __instance.Characters[i];
            if (ch?.Player?.Character is not Denia) continue;

            if (ch.GetNodeOrNull<Sprite2D>("DeniaRestSprite") != null) continue;

            foreach (var child in ch.GetChildren())
            {
                if (child is Node2D n2d && n2d.GetClass() == "SpineSprite")
                    n2d.Visible = false;
            }

            var sprite = new Sprite2D
            {
                Name = "DeniaRestSprite",
                Texture = tex,
                Centered = true,
                Position = new Vector2(0f, -TargetHeight * 0.45f),
                Scale = new Vector2(scale, scale),
                FlipH = i % 2 == 1
            };
            ch.AddChild(sprite);
        }
    }
}
