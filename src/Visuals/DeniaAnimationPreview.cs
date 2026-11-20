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

    internal static bool IsAvailable(DeniaAnimationPreviewKind kind) => kind switch
    {
        DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown or DeniaAnimationPreviewKind.BlackRise
            => DeniaModConfig.UseCodeAnimation,
        DeniaAnimationPreviewKind.BlackTriple or DeniaAnimationPreviewKind.PinkCollapse => !DeniaModConfig.UseCodeAnimation,
        _ => true
    };

    public override void _Ready()
    {
        string? Clip(string video) => DeniaModConfig.UseCodeAnimation ? null : video;
        _visual = Kind switch {
            DeniaAnimationPreviewKind.PinkIdle => new DeniaPinkIdleVisual { VideoClip = Clip("pink_idle") },
            DeniaAnimationPreviewKind.BlackIdle => new DeniaBlackIdleVisual { VideoClip = Clip("dark_idle") },
            DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown => new DeniaPinkStandupVisual(),
            DeniaAnimationPreviewKind.BlackRise => new DeniaBlackRiseVisual(),
            DeniaAnimationPreviewKind.PinkSingle => new DeniaPinkAttackVisual { VideoClip = Clip("pink_1hit") },
            DeniaAnimationPreviewKind.PinkMulti => new DeniaPinkMultiAttackVisual { VideoClip = Clip("pink_2hit") },
            DeniaAnimationPreviewKind.BlackSingle => new DeniaDarkAttackVisual { VideoClip = Clip("dark_1hit") },
            DeniaAnimationPreviewKind.BlackMulti => new DeniaDarkMultiAttackVisual { VideoClip = Clip("dark_2hit") },
            DeniaAnimationPreviewKind.BlackTriple => new DeniaDarkMultiAttackVisual { VideoClip = Clip("dark_3hit") },
            DeniaAnimationPreviewKind.PinkCollapse => new DeniaPinkCollapseVisual { VideoClip = Clip("pink_collapse") },
            DeniaAnimationPreviewKind.BlackFour => new DeniaDarkFourAttackVisual { VideoClip = Clip("dark_4hit") },
            _ => null };
        if (_visual != null)
        {
            AddChild(_visual);
            if (!_visual.Initialized || (_visual.VideoPlaybackRequested && !_visual.UsingVideo))
            {
                _completion.TrySetResult();
                SetProcess(false);
                return;
            }
            if (_visual is DeniaSingleHitVisual attack)
            {
                if (!attack.VideoPlaybackRequested && Kind is (DeniaAnimationPreviewKind.PinkSingle or DeniaAnimationPreviewKind.PinkMulti))
                    attack.PlayWithPinkStandup(Speed, IdleClock);
                else attack.Play(Speed);
                Duration = attack.TotalDuration / Speed;
            }
            else Duration = _visual.PlaybackDuration / PlaybackSpeed;
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
            double clock = Math.Min(Elapsed * PlaybackSpeed, _visual.PlaybackDuration);
            bool reversed = Kind == DeniaAnimationPreviewKind.PinkSitdown;
            _visual.Seek(reversed ? _visual.PlaybackDuration - clock : clock);
            _visual.SyncVideoAudio(_visual.GetVideoAudio(), clock, PlaybackSpeed, false);
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
