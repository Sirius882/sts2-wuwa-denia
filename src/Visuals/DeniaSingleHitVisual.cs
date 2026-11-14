using System;
using System.Threading.Tasks;
using Godot;

namespace Denia;

/// <summary>单段攻击共用的播放、命中等待与中断恢复，素材各自定义时间和身体标定；多段可声明多个命中点。</summary>
public abstract partial class DeniaSingleHitVisual : DeniaFlowIdleVisual
{
    public const string NodeName = "RealtimeSingleHit";
    protected override bool Loop => false;
    public abstract double DamageTime { get; }
    public virtual int ImpactCount => 1;
    public virtual double GetImpactTime(int index) => DamageTime;
    public bool Playing { get; private set; }
    private float _speed = 1;
    private bool _holdAtImpact;
    private TaskCompletionSource[] _impacts = [];
    private int _heldImpact;
    public Task Impact => ImpactAt(0);
    public Task ImpactAt(int index) => index >= 0 && index < _impacts.Length ? _impacts[index].Task : Task.CompletedTask;
    public event Action? Finished;

    public void Play(float speed, bool holdAtImpact = false)
    {
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        foreach (var impact in _impacts) impact.TrySetResult();
        _impacts = new TaskCompletionSource[ImpactCount];
        for (int i = 0; i < _impacts.Length; i++) _impacts[i] = new TaskCompletionSource();
        _heldImpact = 0;
        _holdAtImpact = holdAtImpact;
        _speed = speed;
        Seek(0);
        Playing = true;
        Show();
        SetProcess(true);
    }

    public void ReleaseImpact(Task? expected = null)
    {
        if (_heldImpact < _impacts.Length && (expected == null || ReferenceEquals(expected, _impacts[_heldImpact].Task)))
            _heldImpact++;
    }

    public void ReleaseRemainingImpacts(Task expectedFirst)
    {
        if (ReferenceEquals(expectedFirst, Impact)) _holdAtImpact = false;
    }

    public void Stop()
    {
        _holdAtImpact = false;
        foreach (var impact in _impacts) impact.TrySetResult();
        Playing = false;
        Hide();
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!Playing || !IsVisibleInTree()) return;
        double next = Clock + delta * _speed;
        Seek(_holdAtImpact && _heldImpact < _impacts.Length ? Math.Min(next, GetImpactTime(_heldImpact)) : next);
        for (int i = 0; i < _impacts.Length; i++)
            if (Clock >= GetImpactTime(i)) _impacts[i].TrySetResult();
        if (Clock < DurationSeconds) return;
        Stop();
        Finished?.Invoke();
    }

    public override void _ExitTree() => Stop();
}
