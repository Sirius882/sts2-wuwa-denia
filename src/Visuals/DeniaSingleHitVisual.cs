using System;
using System.Threading.Tasks;
using Godot;

namespace Denia;

/// <summary>单段攻击共用的播放、命中等待与中断恢复，素材各自定义时间和身体标定。</summary>
public abstract partial class DeniaSingleHitVisual : DeniaFlowIdleVisual
{
    public const string NodeName = "RealtimeSingleHit";
    protected override bool Loop => false;
    public abstract double DamageTime { get; }
    public bool Playing { get; private set; }
    private float _speed = 1;
    private bool _holdAtImpact;
    private TaskCompletionSource? _impact;
    public Task Impact => _impact?.Task ?? Task.CompletedTask;
    public event Action? Finished;

    public void Play(float speed, bool holdAtImpact = false)
    {
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        _impact?.TrySetResult();
        _impact = new TaskCompletionSource();
        _holdAtImpact = holdAtImpact;
        _speed = speed;
        Seek(0);
        Playing = true;
        Show();
        SetProcess(true);
    }

    public void ReleaseImpact(Task? expected = null)
    {
        if (expected == null || ReferenceEquals(expected, _impact?.Task)) _holdAtImpact = false;
    }

    public void Stop()
    {
        _holdAtImpact = false;
        _impact?.TrySetResult();
        Playing = false;
        Hide();
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!Playing || !IsVisibleInTree()) return;
        double next = Clock + delta * _speed;
        Seek(_holdAtImpact ? Math.Min(next, DamageTime) : next);
        if (Clock >= DamageTime) _impact?.TrySetResult();
        if (Clock < DurationSeconds) return;
        Stop();
        Finished?.Invoke();
    }

    public override void _ExitTree() => Stop();
}
