using System;
using Godot;

namespace Denia;

/// <summary>两态共用的光流待机时钟、资源与动作显隐绑定；各自保留原素材的循环和特效。</summary>
public abstract partial class DeniaFlowIdleVisual : Node2D
{
    protected abstract string Assets { get; }
    protected abstract string ShaderPath { get; }
    public abstract double DurationSeconds { get; }
    public abstract int FrameCount { get; }
    public abstract float LayoutHeight { get; }
    public abstract Vector2 DesignOrigin { get; }
    protected virtual bool WrapFrames => false;
    protected virtual bool Loop => true;
    protected virtual bool UsesTracking => true;
    protected virtual Vector2 CanvasSize => new(592, 600);
    protected ShaderMaterial FlowMaterial = null!;
    protected float[] BubbleCy = null!, OrbX = null!, OrbY = null!;
    protected bool Flip;
    private ColorRect _body = null!;
    private AnimatedSprite2D? _actions;
    private bool _suppressed;
    private double _clock;

    public double Clock => _clock;
    public bool Initialized { get; private set; }

    public override void _Ready()
    {
        var tracking = GD.Load<Resource>(Assets + "tracking.tres")
            ?? throw new InvalidOperationException($"Idle tracking resource is missing: {Assets}");
        if (UsesTracking)
        {
            BubbleCy = tracking.GetMeta("bubble_cy").AsFloat32Array();
            OrbX = tracking.GetMeta("orb_x").AsFloat32Array();
            OrbY = tracking.GetMeta("orb_y").AsFloat32Array();
            if (BubbleCy.Length != FrameCount || OrbX.Length != FrameCount || OrbY.Length != FrameCount)
                throw new InvalidOperationException($"Idle requires {FrameCount} tracking samples: {Assets}");
        }
        FlowMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        var frames = GD.Load<Texture2D>(Assets + "frames.webp");
        var flow = GD.Load<Texture2D>(Assets + "flow.png");
        if (FlowMaterial.Shader == null || frames == null || flow == null)
            throw new InvalidOperationException($"Idle shader or textures are missing: {Assets}");
        FlowMaterial.SetShaderParameter("frames", frames);
        FlowMaterial.SetShaderParameter("flow_fields", flow);
        _body = new ColorRect {
            Name = "FlowBody", Position = -DesignOrigin, Size = CanvasSize,
            MouseFilter = Control.MouseFilterEnum.Ignore, Material = FlowMaterial, ShowBehindParent = true };
        AddChild(_body);
        Seek(0);
        Initialized = true;
    }

    public void BindActions(AnimatedSprite2D actions)
    {
        _actions = actions;
        actions.AnimationChanged += () => {
            SyncAction();
            // AnimatedSprite.Play 在信号返回后才恢复播放，下一轮再暂停旧idle占位。
            Callable.From(SyncAction).CallDeferred();
        };
        SyncAction();
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        SyncAction();
    }

    private void SyncAction()
    {
        if (_actions == null || !GodotObject.IsInstanceValid(_actions)) return;
        bool idle = _actions.Animation == "idle";
        _actions.Visible = !idle && !_suppressed;
        Visible = idle && !_suppressed;
        // 原 idle 只保留首帧作场景加载占位，不再运行旧待机帧图集。
        if (idle) _actions.Pause();
    }

    public void SetFacing(bool flip)
    {
        Flip = flip;
        FlowMaterial.SetShaderParameter("flip_h", flip);
        // 黑态原素材的内容中心偏左，镜像必须围绕角色锚点而非画布中央。
        _body.Position = flip ? new Vector2(DesignOrigin.X - CanvasSize.X, -DesignOrigin.Y) : -DesignOrigin;
        QueueRedraw();
    }

    public void Seek(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        _clock = Loop ? ((seconds % DurationSeconds) + DurationSeconds) % DurationSeconds
            : Math.Clamp(seconds, 0, DurationSeconds);
        double frame = _clock / DurationSeconds * (WrapFrames ? FrameCount : FrameCount - 1);
        int index = Math.Min((int)frame, FrameCount - 1);
        FlowMaterial.SetShaderParameter("frame_index", index);
        FlowMaterial.SetShaderParameter("fraction", (float)(frame - index));
        UpdateFlowParameters();
        QueueRedraw();
    }

    protected virtual void UpdateFlowParameters() { }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) Seek(_clock + delta);
    }

    protected float Track(float[] values)
    {
        double frame = _clock / DurationSeconds * (WrapFrames ? FrameCount : FrameCount - 1);
        int i = Math.Min((int)frame, FrameCount - 1);
        float f = (float)(frame - i);
        int Index(int n) => WrapFrames ? (n + FrameCount) % FrameCount : Math.Clamp(n, 0, FrameCount - 1);
        float a = values[Index(i - 1)], b = values[i], c = values[Index(i + 1)], d = values[Index(i + 2)];
        return 0.5f * (2 * b + (c - a) * f + (2 * a - 5 * b + 4 * c - d) * f * f
            + (-a + 3 * b - 3 * c + d) * f * f * f);
    }
}
