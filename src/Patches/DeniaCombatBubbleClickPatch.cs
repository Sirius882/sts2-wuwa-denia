using Godot;
using HarmonyLib;
using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;

namespace Denia;

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
public static class DeniaCombatBubbleClickPatch
{
    private static void Postfix(CombatState state)
    {
        try
        {
            // 不回退到首个玩家：队友是达妮娅不能替本机玩家启用特效。
            Player? me = LocalContext.GetMe(state);
            var room = NCombatRoom.Instance;
            if (!IsLocalDenia(me) || room == null || room.GetNodeOrNull("DeniaBubbleClicks") != null) return;
            var listener = new DeniaCombatBubbleClicks { Name = "DeniaBubbleClicks", Room = room, Player = me };
            room.AddChild(listener);
            room.MoveChild(listener, 0); // _Input逆序遍历，让房间已有输入处理器先运行。
        }
        catch (System.Exception ex) { GD.PrintErr($"[Denia] Click bubbles unavailable: {ex.Message}"); }
    }

    internal static bool IsLocalDenia([NotNullWhen(true)] Player? player) => player?.Character is Denia && LocalContext.IsMe(player);
}

public partial class DeniaCombatBubbleClicks : Node2D
{
    public NCombatRoom Room { get; init; } = null!;
    public Player Player { get; init; } = null!;

    public override void _Input(InputEvent input)
    {
        try
        {
            if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
            if (!IsVisibleInTree() || GetTree().Paused || !DeniaCombatBubbleClickPatch.IsLocalDenia(Player)
                || Room.Mode != CombatRoomMode.ActiveCombat || !CombatManager.Instance.IsInProgress
                || CombatManager.Instance.IsOverOrEnding || !ActiveScreenContext.Instance.IsCurrent(Room)
                || Room.Ui.Hand.InCardPlay || Room.Ui.Hand.IsInCardSelection || NTargetManager.Instance.IsInSelection) return;
            if (!DeniaBlankClick.IsBlank(GetViewport().GuiGetHoveredControl())) return;
            var burst = new DeniaBubbleBurst {
                IsBlack = DeniaFormHelper.IsBlack(Player.Creature),
                Position = Room.CombatVfxContainer.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position,
                Scale = Vector2.One * (Room.Size.Y / 600f) };
            Room.CombatVfxContainer.AddChild(burst);
            // 纯表现旁听，不消费点击；现有UI／战斗输入继续原样执行。
        }
        catch (System.Exception ex) { GD.PrintErr($"[Denia] Click bubbles skipped: {ex.Message}"); }
    }
}
