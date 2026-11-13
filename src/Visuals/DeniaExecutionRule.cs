using System;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using TuneStrain;

namespace Denia;

/// <summary>公开破坏规则在原伤害前插入表现；不参与伤害或处决奖励计算。</summary>
internal sealed class DeniaExecutionRule : IAemeathResonanceBreakRule
{
    private const string NodeName = "DeniaExecutionOverlay";
    internal static readonly DeniaExecutionRule Instance = new();
    public int Priority => -100;

    public bool ShouldApply(AemeathResonanceBreakContext context) =>
        DeniaExecutionTimeline.ShouldPlay(TuneStrainState.GetBias(context.Target), context.Target.IsDead,
            context.Target.GetPower<IllusionPower>() != null);

    public Task Apply(AemeathResonanceBreakContext context) => Play(context.Target);

    internal static async Task Play(Creature? target)
    {
        if (NonInteractiveMode.IsActive) return;
        var room = NCombatRoom.Instance;
        if (!GodotObject.IsInstanceValid(room) || !room!.IsInsideTree()) return;
        DeniaExecutionOverlay? overlay = null;
        try
        {
            // 前次命中后的退场可能尚未结束；移除其视觉后按原命令顺序播放下一次。
            var previous = room.GetNodeOrNull<DeniaExecutionOverlay>(NodeName);
            if (GodotObject.IsInstanceValid(previous))
            {
                room.RemoveChild(previous);
                previous.QueueFree();
            }
            var targetNode = target == null ? null : room.GetCreatureNode(target);
            Vector2 size = room.GetViewportRect().Size;
            overlay = new DeniaExecutionOverlay { Name = NodeName,
                TargetUv = targetNode == null ? new Vector2(0.7f, 0.5f)
                    : targetNode.GetGlobalTransformWithCanvas().Origin / size };
            room.AddChild(overlay);
            var waiting = room.GetNodeOrNull("WaitingForOtherPlayers");
            if (waiting != null) room.MoveChild(overlay, waiting.GetIndex());
            // 所有模式原速演出；Cmd.Wait 会在 Instant 下跳过，因此使用同样的 Godot timer 等待。
            var timer = room.GetTree().CreateTimer(DeniaExecutionTimeline.Release);
            await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout).ToTask().WaitAsync(overlay.ExitToken);
            if (GodotObject.IsInstanceValid(overlay)) overlay.RevealGame();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            GD.PrintErr($"[DeniaExecution] Presentation skipped: {exception.Message}");
            if (GodotObject.IsInstanceValid(overlay)) overlay!.QueueFree();
        }
    }
}
