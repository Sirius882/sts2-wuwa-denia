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
    private ColorRect? _body;
    private DeniaBattleVideo? _video;
    private bool _requestedVideo;
    private bool _videoUnavailable;
    internal string? VideoClip { get; init; }
    internal bool LoadVideoOnReady { get; init; } = true;
    internal bool VideoPlaybackRequested => _requestedVideo;
    internal bool UsingVideo => _video != null && _requestedVideo && !_videoUnavailable;
    internal bool HasBody => _requestedVideo ? _video != null : _body != null;
    internal double PlaybackDuration => _video != null && _requestedVideo ? _video.Duration : DurationSeconds;
    protected double[]? VideoImpacts => _video?.Impacts;
    private AudioStreamPlayer? _videoAudio;
    private AudioStream? _playingStream;
    private float _videoAudioSpeed = 1;
    private double _lastAudioClock;
    internal AudioStream? GetVideoAudio(bool reversed = false) => UsingVideo ? _video!.GetAudio(reversed) : null;
    private double _clock;

    public double Clock => _clock;
    public bool Initialized { get; private set; }

    public override void _Ready()
    {
        SetVideoPlayback(LoadVideoOnReady && VideoClip != null);
        Initialized = true;
    }

    internal void SetVideoPlayback(bool enable)
    {
        if (Initialized && _requestedVideo == enable) return;
        _requestedVideo = enable;
        if (!enable) StopVideoAudio();
        if (enable && VideoClip != null && _video == null && !_videoUnavailable)
        {
            _video = DeniaBattleVideo.TryOpen(VideoClip, LayoutHeight);
            if (_video != null) AddChild(_video.Sprite);
            else _videoUnavailable = true;
        }
        if (!enable && _body == null) InitializeCodeBody();
        if (_body != null) _body.Visible = !enable;
        if (_video != null) _video.Sprite.Visible = enable;
        SetFacing(Flip);
        Seek(_clock);
    }

    private void InitializeCodeBody()
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
    }

    public virtual void SetFacing(bool flip)
    {
        Flip = flip;
        if (_requestedVideo) _video?.SetFacing(flip);
        else if (_body != null)
        {
            FlowMaterial.SetShaderParameter("flip_h", flip);
            // 黑态原素材的内容中心偏左，镜像必须围绕角色锚点而非画布中央。
            _body.Position = flip ? new Vector2(DesignOrigin.X - CanvasSize.X, -DesignOrigin.Y) : -DesignOrigin;
        }
        QueueRedraw();
    }

    protected void SetBodyOpacity(float alpha)
    {
        var color = new Color(1, 1, 1, alpha);
        if (_body != null) _body.Modulate = color;
        if (_video != null) _video.Sprite.Modulate = color;
    }

    public void Seek(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        double duration = PlaybackDuration;
        _clock = Loop ? ((seconds % duration) + duration) % duration : Math.Clamp(seconds, 0, duration);
        if (_requestedVideo)
        {
            if (!UsingVideo) return;
            try { _video!.Seek(_clock, Flip); }
            catch (Exception exception)
            {
                GD.PrintErr($"[DeniaBattleVideo] {VideoClip}: {exception.Message}");
                StopVideoAudio();
                _videoUnavailable = true;
            }
            return;
        }
        double frame = _clock / PlaybackDuration * (WrapFrames ? FrameCount : FrameCount - 1);
        int index = Math.Min((int)frame, FrameCount - 1);
        FlowMaterial.SetShaderParameter("frame_index", index);
        FlowMaterial.SetShaderParameter("fraction", (float)(frame - index));
        UpdateFlowParameters();
        QueueRedraw();
    }

    protected virtual void UpdateFlowParameters() { }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree())
        {
            Seek(_clock + delta);
            if (UsingVideo) SyncVideoAudio(GetVideoAudio(), _clock, 1, false);
        }
        else StopVideoAudio();
    }

    internal void SyncVideoAudio(AudioStream? stream, double clock, float speed, bool held)
    {
        if (stream == null)
        {
            StopVideoAudio();
            return;
        }
        if (_videoAudio == null)
        {
            _videoAudio = new AudioStreamPlayer { Name = "VideoAudio", Bus = "SFX" };
            AddChild(_videoAudio);
        }
        if (!ReferenceEquals(_playingStream, stream) || clock < _lastAudioClock)
        {
            _playingStream = stream;
            _videoAudio.Stream = stream;
            _videoAudioSpeed = speed;
            _videoAudio.PitchScale = speed;
            _videoAudio.Play((float)clock);
        }
        if (_videoAudio.Playing)
        {
            // Correct drift after a slow render frame and pause the soundtrack while a damage callback holds the picture.
            double position = _videoAudio.GetPlaybackPosition();
            if (Math.Abs(position - clock) > 0.075 * _videoAudioSpeed)
                _videoAudio.Seek((float)clock);
            _videoAudio.StreamPaused = held;
        }
        _lastAudioClock = clock;
    }

    protected void StopVideoAudio()
    {
        if (_videoAudio == null || !GodotObject.IsInstanceValid(_videoAudio)) return;
        _videoAudio.Stop();
        _playingStream = null;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) _video?.Dispose();
    }

    protected float Track(float[] values)
    {
        double frame = _clock / PlaybackDuration * (WrapFrames ? FrameCount : FrameCount - 1);
        int i = Math.Min((int)frame, FrameCount - 1);
        float f = (float)(frame - i);
        int Index(int n) => WrapFrames ? (n + FrameCount) % FrameCount : Math.Clamp(n, 0, FrameCount - 1);
        float a = values[Index(i - 1)], b = values[i], c = values[Index(i + 1)], d = values[Index(i + 2)];
        return 0.5f * (2 * b + (c - a) * f + (2 * a - 5 * b + 4 * c - d) * f * f
            + (-a + 3 * b - 3 * c + d) * f * f * f);
    }
}
