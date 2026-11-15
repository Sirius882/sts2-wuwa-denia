using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

[HarmonyPatch(typeof(NMapDrawings), "CreateLineForPlayer", new[] { typeof(Player), typeof(bool) })]
internal static class DeniaMapDrawingColorPatch
{
    private static void Postfix(Player player, bool isErasing, Line2D __result)
    {
        if (!isErasing && player.Character is Denia && GodotObject.IsInstanceValid(__result))
            DeniaMapBrushVisual.Style(__result);
    }
}

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.Initialize))]
internal static class DeniaMapDrawingBubblePatch
{
    private static void Postfix(NMapDrawings __instance, IPlayerCollection playerCollection)
    {
        Player? me = LocalContext.GetMe(playerCollection);
        if (!DeniaCombatBubbleClickPatch.IsLocalDenia(me) || __instance.GetNodeOrNull("DeniaMapBrush") != null) return;
        __instance.AddChild(new DeniaMapBrushListener { Name = "DeniaMapBrush", Drawings = __instance, Player = me });
    }
}

public partial class DeniaMapBrushListener : DeniaMapBrushVisual
{
    public NMapDrawings Drawings { get; init; } = null!;
    public Player Player { get; init; } = null!;

    public override void _Process(double delta)
    {
        bool drawing = IsVisibleInTree() && !GetTree().Paused && DisplayServer.WindowIsFocused()
            && DeniaCombatBubbleClickPatch.IsLocalDenia(Player)
            && Input.IsMouseButtonPressed(MouseButton.Left) && Drawings.IsLocalDrawing()
            && Drawings.GetLocalDrawingMode(useOverride: true) == DrawingMode.Drawing;
        Advance(delta, drawing, GetLocalMousePosition(), GetViewportRect().Size.Y / 1080f / Mathf.Max(0.01f, GlobalScale.Abs().Y));
    }
}
