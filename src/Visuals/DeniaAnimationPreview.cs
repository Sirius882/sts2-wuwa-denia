using System;
using System.Threading.Tasks;
using Godot;

namespace Denia;

/// <summary>复用生产视觉的单次预览时钟；不创建攻击命令、不修改形态 Power。</summary>
internal partial class DeniaAnimationPreview : Node2D
{
    internal DeniaAnimationPreviewKind Kind { get; init; }
    internal float Speed { get; init; } = 1;
    internal double IdleClock { get; init; }
    internal double Elapsed { get; private set; }
    internal double Duration { get; private set; }
    internal float LayoutHeight => _visual?.LayoutHeight ?? 350;
    internal bool ReplacesBody => _visual != null;
    private DeniaFlowIdleVisual? _visual;
    private bool? _facing;
    private readonly TaskCompletionSource _completion = new();
    internal Task Completion => _completion.Task;

    public override void _Ready()
    {
        _visual = Kind switch {
            DeniaAnimationPreviewKind.PinkIdle => new DeniaPinkIdleVisual(),
            DeniaAnimationPreviewKind.BlackIdle => new DeniaBlackIdleVisual(),
            DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown => new DeniaPinkStandupVisual(),
            DeniaAnimationPreviewKind.BlackRise => new DeniaBlackRiseVisual(),
            DeniaAnimationPreviewKind.PinkSingle => new DeniaPinkAttackVisual(),
            DeniaAnimationPreviewKind.PinkMulti => new DeniaPinkMultiAttackVisual(),
            DeniaAnimationPreviewKind.BlackSingle => new DeniaDarkAttackVisual(),
            DeniaAnimationPreviewKind.BlackMulti => new DeniaDarkMultiAttackVisual(),
            DeniaAnimationPreviewKind.BlackFour => new DeniaDarkFourAttackVisual(),
            _ => null };
        if (_visual != null)
        {
            AddChild(_visual);
            if (!_visual.Initialized) throw new InvalidOperationException("Animation preview did not initialize.");
            if (_visual is DeniaSingleHitVisual attack)
            {
                if (Kind is DeniaAnimationPreviewKind.PinkSingle or DeniaAnimationPreviewKind.PinkMulti)
                    attack.PlayWithPinkStandup(Speed, IdleClock);
                else attack.Play(Speed);
                Duration = attack.TotalDuration / Speed;
            }
            else Duration = _visual.DurationSeconds / PlaybackSpeed;
            _visual.SetProcess(false);
            Advance(0);
        }
        else
        {
            bool map = Kind == DeniaAnimationPreviewKind.MapBubble;
            Duration = 1.5 / (map ? 0.55 : 1);
            AddChild(new DeniaBubbleBurst { IsBlack = Kind == DeniaAnimationPreviewKind.BlackBubble,
                Position = new Vector2(0, -175), Scale = Vector2.One * (map ? 0.65f : 1),
                PlaybackSpeed = map ? 0.55f : 1 });
        }
    }

    private float PlaybackSpeed => Kind is DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown ? Speed : 1;
    internal void SetFacing(bool flip)
    {
        if (_facing == flip) return;
        _facing = flip;
        _visual?.SetFacing(flip);
    }

    private void Advance(double delta)
    {
        if (_visual is DeniaSingleHitVisual attack) attack._Process(delta);
        else if (_visual != null)
        {
            double clock = Math.Min(Elapsed * PlaybackSpeed, _visual.DurationSeconds);
            _visual.Seek(Kind == DeniaAnimationPreviewKind.PinkSitdown ? _visual.DurationSeconds - clock : clock);
        }
    }

    public override void _Process(double delta)
    {
        Elapsed += delta;
        Advance(delta);
        if (Elapsed >= Duration) { _completion.TrySetResult(); SetProcess(false); }
    }

    public override void _ExitTree() => _completion.TrySetResult();
}
