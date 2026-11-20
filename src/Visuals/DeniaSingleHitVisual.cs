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
    internal int PlaybackImpactCount => VideoImpacts is { Length: > 0 } impacts ? impacts.Length : ImpactCount;
    // 视频提供原片命中点；代码模式使用各动作自己的命中点。
    internal int DamageImpactCount => PlaybackImpactCount;
    internal double GetPlaybackImpactTime(int index) => VideoImpacts is { Length: > 0 } impacts
        ? impacts[index] : GetImpactTime(index);
    protected virtual Vector2 PinkStandupEndOrigin => new(247.5f, 750);
    public bool Playing { get; private set; }
    private float _speed = 1;
    private bool _holdAtImpact;
    public bool CanBeInterrupted => !Playing || !_holdAtImpact || _heldImpact >= _impacts.Length;
    private TaskCompletionSource[] _impacts = [];
    private int _heldImpact;
    private DeniaPinkStandupVisual? _standup;
    private DeniaPinkIdleVisual? _seated;
    private SubViewport? _standupView, _seatedView, _attackView;
    private ColorRect? _handoff, _attackSnapshot;
    private ShaderMaterial? _handoffMaterial;
    private double _leadInElapsed;
    private double _returnElapsed;
    private double _idleStartClock;
    public double LeadInDuration { get; private set; }
    public double ReturnDuration => LeadInDuration;
    public double TotalDuration => LeadInDuration + PlaybackDuration + ReturnDuration;
    public double TimelineClock => _leadInElapsed + Clock + _returnElapsed;
    public double IdleHandoffClock => _idleStartClock + TimelineClock;
    public bool IsPreparing => LeadInDuration > 0 && _leadInElapsed < LeadInDuration;
    public bool IsReturning => Playing && ReturnDuration > 0 && Clock >= PlaybackDuration;
    public Task Impact => ImpactAt(0);
    public Task ImpactAt(int index) => index >= 0 && index < _impacts.Length ? _impacts[index].Task : Task.CompletedTask;
    public event Action? Finished;

    public void Play(float speed, bool holdAtImpact = false)
    {
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        foreach (var impact in _impacts) impact.TrySetResult();
        _impacts = new TaskCompletionSource[DamageImpactCount];
        for (int i = 0; i < _impacts.Length; i++) _impacts[i] = new TaskCompletionSource();
        StopVideoAudio();
        _heldImpact = 0;
        _holdAtImpact = holdAtImpact;
        _speed = speed;
        LeadInDuration = _leadInElapsed = _returnElapsed = 0;
        SetPreparationVisibility(false);
        SetAttackOpacity(1);
        Seek(0);
        Playing = true;
        Show();
        SetProcess(true);
    }

    public void PlayWithPinkStandup(float speed, double idleClock, bool holdAtImpact = false)
    {
        if (!GodotObject.IsInstanceValid(_standup))
        {
            _standupView = CreateHandoffView();
            _standupView.GuiSnapControlsToPixels = false;
            _seatedView = CreateHandoffView();
            _attackView = CreateHandoffView();
            _standup = new DeniaPinkStandupVisual { Name = "Standup", Visible = false };
            _standupView.AddChild(_standup);
            _standup.SetProcess(false);
            _seated = new DeniaPinkIdleVisual { Name = "SeatedHandoff", Visible = false };
            _seatedView.AddChild(_seated);
            _seated.SetProcess(false);
            _attackSnapshot = new ColorRect { Size = CanvasSize,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            _attackView.AddChild(_attackSnapshot);
            _handoffMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/denia_pink_handoff.gdshader") };
            _handoff = new ColorRect { Name = "PinkHandoff", Material = _handoffMaterial,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_handoff);
        }
        Play(speed, holdAtImpact);
        _idleStartClock = idleClock;
        LeadInDuration = _standup!.PlaybackDuration;
        SetFacing(Flip);
        SeekTimeline(0);
    }

    public override void SetFacing(bool flip)
    {
        base.SetFacing(flip);
        if (GodotObject.IsInstanceValid(_standup)) _standup!.SetFacing(flip);
        if (GodotObject.IsInstanceValid(_seated)) _seated!.SetFacing(flip);
        UpdatePreparation();
    }

    /// <summary>总时间轴由起身、原攻击和倒放起身构成，命中坐标仍使用攻击自身时钟。</summary>
    public void SeekTimeline(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        _leadInElapsed = Math.Clamp(seconds, 0, LeadInDuration);
        _returnElapsed = Math.Clamp(seconds - LeadInDuration - PlaybackDuration, 0, ReturnDuration);
        Seek(Math.Max(0, seconds - LeadInDuration));
        UpdatePreparation();
    }

    private void SetPreparationVisibility(bool visible)
    {
        if (GodotObject.IsInstanceValid(_standup)) _standup!.Visible = visible;
        if (GodotObject.IsInstanceValid(_seated)) _seated!.Visible = visible;
        if (GodotObject.IsInstanceValid(_handoff)) _handoff!.Visible = visible;
        foreach (var view in new[] { _standupView, _seatedView, _attackView })
            if (GodotObject.IsInstanceValid(view)) view!.RenderTargetUpdateMode = visible
                ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }

    private SubViewport CreateHandoffView()
    {
        var view = new SubViewport { TransparentBg = true, Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        AddChild(view);
        return view;
    }

    private void ConfigureHandoffViews()
    {
        // 按最终屏幕像素渲染，避免多段动作的父级放大使交接纹理二次采样变糊。
        Vector2 scale = GlobalScale.Abs();
        Vector2 origin = (DesignOrigin * scale).Ceil() + Vector2.One * 64
            + GlobalPosition - GlobalPosition.Floor();
        Vector2I size = (Vector2I)(CanvasSize * scale + Vector2.One * 128).Ceil();
        foreach (var view in new[] { _standupView!, _seatedView!, _attackView! })
        {
            view.Size = size;
            view.CanvasTransform = new Transform2D(0, scale, 0, origin);
        }
        _handoff!.Position = -origin / scale;
        _handoff.Size = (Vector2)size / scale;
        _attackSnapshot!.Material = FlowMaterial;
        _attackSnapshot.Position = Flip ? new Vector2(DesignOrigin.X - CanvasSize.X, -DesignOrigin.Y) : -DesignOrigin;
    }

    private void SetAttackOpacity(float alpha)
    {
        SetBodyOpacity(alpha);
        // SelfModulate只作用于本节点额外绘制的攻击特效，不影响起身／坐姿子节点。
        SelfModulate = new Color(1, 1, 1, alpha);
    }

    private void UpdatePreparation()
    {
        if (!GodotObject.IsInstanceValid(_standup) || !(IsPreparing || IsReturning))
        {
            SetPreparationVisibility(false);
            SetAttackOpacity(1);
            return;
        }
        float t = (float)(IsReturning ? ReturnDuration - _returnElapsed : _leadInElapsed);
        float entry = Mathf.SmoothStep(0, 1, Mathf.Clamp(t / 0.14f, 0, 1));
        float exit = Mathf.SmoothStep(0, 1, Mathf.Clamp((t - (float)LeadInDuration + 0.16f) / 0.16f, 0, 1));
        ConfigureHandoffViews();
        // 先对齐待机坐姿，再连续过渡到攻击侧身站姿的脚底锚点与身高。
        float align = Mathf.SmoothStep(0, 1, Mathf.Clamp((t - 0.3f) / 2.4f, 0, 1));
        float height = Mathf.Lerp(DeniaPinkStandupVisual.SeatedHeight, _standup!.LayoutHeight, align);
        Vector2 origin = DeniaPinkStandupVisual.SeatedOrigin.Lerp(PinkStandupEndOrigin, align);
        float scale = LayoutHeight / height;
        _standup.Scale = Vector2.One * scale;
        var offset = (_standup.DesignOrigin - origin) * scale;
        _standup.Position = new Vector2(Flip ? -offset.X : offset.X, offset.Y);
        _standup.Seek(t);
        _standup.Show();
        _seated!.Scale = Vector2.One * (LayoutHeight / _seated.LayoutHeight);
        _seated.Seek(IdleHandoffClock);
        _seated.Visible = entry < 1;
        _standupView!.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _seatedView!.RenderTargetUpdateMode = entry < 1 ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _attackView!.RenderTargetUpdateMode = exit > 0 ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _handoffMaterial!.SetShaderParameter("first", entry < 1 ? _seatedView.GetTexture() : _standupView.GetTexture());
        _handoffMaterial.SetShaderParameter("second", entry < 1 ? _standupView.GetTexture() : _attackView.GetTexture());
        _handoffMaterial.SetShaderParameter("weight", entry < 1 ? entry : exit);
        _handoff!.Show();
        SetAttackOpacity(0);
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
        StopVideoAudio();
        _holdAtImpact = false;
        foreach (var impact in _impacts) impact.TrySetResult();
        Playing = false;
        _leadInElapsed = LeadInDuration;
        _returnElapsed = ReturnDuration;
        SetPreparationVisibility(false);
        SetAttackOpacity(1);
        Hide();
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!Playing || !IsVisibleInTree())
        {
            StopVideoAudio();
            return;
        }
        double advance = delta * _speed;
        var first = Impact;
        if (IsPreparing)
        {
            double used = Math.Min(advance, LeadInDuration - _leadInElapsed);
            _leadInElapsed += used;
            advance -= used;
            UpdatePreparation();
        }
        if (Clock < PlaybackDuration)
        {
            double before = Clock;
            double next = Clock + advance;
            Seek(_holdAtImpact && _heldImpact < _impacts.Length ? Math.Min(next, GetPlaybackImpactTime(_heldImpact)) : next);
            advance = Clock >= PlaybackDuration ? Math.Max(0, advance - (Clock - before)) : 0;
        }
        if (VideoPlaybackRequested && !UsingVideo)
        {
            Stop();
            Finished?.Invoke();
            return;
        }
        for (int i = 0; i < _impacts.Length && ReferenceEquals(first, Impact); i++)
            if (Clock >= GetPlaybackImpactTime(i)) _impacts[i].TrySetResult();
        if (!Playing || !ReferenceEquals(first, Impact)) return;
        bool held = _holdAtImpact && _heldImpact < _impacts.Length && Clock >= GetPlaybackImpactTime(_heldImpact);
        if (IsPreparing) return;
        if (Clock < PlaybackDuration)
        {
            SyncVideoAudio(GetVideoAudio(), Clock, _speed, held);
            return;
        }
        if (ReturnDuration > 0)
        {
            _returnElapsed = Math.Min(ReturnDuration, _returnElapsed + advance);
            UpdatePreparation();
            if (_returnElapsed < ReturnDuration) return;
        }
        Stop();
        Finished?.Invoke();
    }

    public override void _ExitTree() => Stop();
}
