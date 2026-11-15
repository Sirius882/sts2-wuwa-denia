using System;
using System.Runtime.CompilerServices;
using System.Threading;
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
    private static readonly ConditionalWeakTable<NCombatRoom, SemaphoreSlim> RoomQueues = new();
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
        var queue = RoomQueues.GetValue(room, _ => new SemaphoreSlim(1, 1));
        await queue.WaitAsync();
        DeniaExecutionOverlay? overlay = null;
        try
        {
            if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
            // 完整画面不可打断；前次已结束的画面只留下音轨尾段，不因下一次处决被移除。
            var previous = room.GetNodeOrNull<DeniaExecutionOverlay>(NodeName);
            if (GodotObject.IsInstanceValid(previous))
                previous.Name = $"{NodeName}AudioTail{previous.GetInstanceId()}";
            var targetNode = target == null ? null : room.GetCreatureNode(target);
            Vector2 size = room.GetViewportRect().Size;
            overlay = new DeniaExecutionOverlay { Name = NodeName,
                TargetUv = targetNode == null ? new Vector2(0.7f, 0.5f)
                    : targetNode.GetGlobalTransformWithCanvas().Origin / size };
            room.AddChild(overlay);
            // 所有模式原速演出；Cmd.Wait 会在 Instant 下跳过，因此使用同样的 Godot timer 等待。
            var timer = room.GetTree().CreateTimer(DeniaExecutionTimeline.Release);
            await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout).ToTask().WaitAsync(overlay.ExitToken);
            if (GodotObject.IsInstanceValid(overlay)) overlay.RevealGame();
            // 放行须等Return真正播放结束；不使用固定Total计时，以免慢帧提前结束画面等待。
            await overlay.VisualCompletion.WaitAsync(overlay.ExitToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            GD.PrintErr($"[DeniaExecution] Presentation skipped: {exception.Message}");
            if (GodotObject.IsInstanceValid(overlay)) overlay!.QueueFree();
        }
        finally { queue.Release(); }
    }
}
