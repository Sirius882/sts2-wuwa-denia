using System;
using System.Threading;
using Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace Denia;

/// <summary>房间拥有的全屏演出：粉白滤镜入场 + 原片姜饼人／人物 + 代码冲击。</summary>
public partial class DeniaExecutionOverlay : CanvasLayer
{
    private static readonly Vector2 Design = new(1920, 886);
    private readonly CancellationTokenSource _exit = new();
    private Control _inputBlocker = null!;
    private Vector2 _size;
    private AnimationPlayer _player = null!;
    private VideoStreamPlayer _video = null!;
    private AudioStreamPlayer _audio = null!;
    private TextureRect _poster = null!;
    private ColorRect _screen = null!;
    private DeniaExecutionInk _ink = null!;
    private ShaderMaterial _screenMaterial = null!;
    private ShaderMaterial _impactMaterial = null!;
    private ModAnimStateMachine? _machine;
    private GodotAnimationPlayerBackend? _backend;
    private Rect2 _frame;
    private bool _videoStarted;
    private bool _returning;
    private bool _visualFinished;
    private int _warmFrames;
    private double _age;
    private float _frameClock;

    [Export] public float Clock { get; set; }
    public Vector2 TargetUv { get; set; } = new(0.7f, 0.5f);
    public CancellationToken ExitToken => _exit.Token;

    public override void _Ready()
    {
        try
        {
            // 独立于房间的变换／裁切，覆盖HUD、控制台和嵌入式浮窗（1024层）。
            Layer = 1025;
            _inputBlocker = new Control { Name = "InputBlocker", MouseFilter = Control.MouseFilterEnum.Stop };
            AddChild(_inputBlocker);
            AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            _screenMaterial = new ShaderMaterial {
                Shader = GD.Load<Shader>("res://shaders/denia_execution_screen.gdshader") };
            _screen = new ColorRect { Material = _screenMaterial, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_screen);
            _video = new VideoStreamPlayer {
                Name = "VideoStreamPlayer",
                ProcessPriority = 100,
                Stream = GD.Load<VideoStream>(DeniaExecutionTimeline.VideoPath),
                Expand = true, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            if (_video.Stream == null) throw new InvalidOperationException("Execution video is missing");
            AddChild(_video);
            // 提前解码少量首帧，始终藏在代码转场后；进入人物段时从暂停处继续。
            _video.Play();
            _impactMaterial = new ShaderMaterial {
                Shader = GD.Load<Shader>("res://shaders/denia_execution_impact.gdshader") };
            _poster = new TextureRect { Material = _impactMaterial, Visible = false,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_poster);
            _ink = new DeniaExecutionInk { Director = this, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_ink);
            _player = new AnimationPlayer { Name = "AnimationPlayer", ProcessPriority = -100 };
            AddChild(_player);
            var library = new AnimationLibrary();
            AddClip(library, "Execution", 0, DeniaExecutionTimeline.Release);
            AddClip(library, "Return", DeniaExecutionTimeline.Release, DeniaExecutionTimeline.Return);
            _player.AddAnimationLibrary("", library);
            _backend = new GodotAnimationPlayerBackend(_player);
            _backend.Completed += id => {
                if (id != "Return") return;
                _visualFinished = true;
                Hide();
                _inputBlocker.MouseFilter = Control.MouseFilterEnum.Ignore;
                _video.Stop();
            };
            var builder = ModAnimStateMachineBuilder.Create();
            // 入场／人物／冲击共用一条时钟，避免逐段切换累计一帧的误差。
            builder.AddState("Execution");
            // 冲击末白帧等待游戏命令的固定时长，解码／状态机回调不驱动伤害。
            builder.AddState("Return");
            builder.AddAnyState("Release", "Return");
            _machine = builder.Build(_backend);
            // 音轨已提前裁到滤镜起点；不跟随视频预解码暂停，也不等视频显现才播放。
            _audio = new AudioStreamPlayer { Name = "ExecutionAudio", Bus = "SFX",
                Stream = GD.Load<AudioStream>(DeniaExecutionTimeline.AudioPath) };
            if (_audio.Stream == null) throw new InvalidOperationException("Execution audio is missing");
            AddChild(_audio);
            _audio.Play();
            UpdateFrame();
        }
        catch (Exception ex) { Abort(ex); }
    }

    private static void AddClip(AnimationLibrary library, string name, float start, float duration)
    {
        var animation = new Animation { Length = duration };
        int track = animation.AddTrack(Animation.TrackType.Value);
        animation.TrackSetPath(track, new NodePath(".:Clock"));
        animation.TrackInsertKey(track, 0, start);
        animation.TrackInsertKey(track, duration, start + duration);
        library.AddAnimation(name, animation);
    }

    public void RevealGame()
    {
        if (_returning || !IsInsideTree() || IsQueuedForDeletion()) return;
        _returning = true;
        Clock = DeniaExecutionTimeline.Release;
        _machine?.SetTrigger("Release");
    }

    public override void _Process(double delta)
    {
        try
        {
            _age += delta;
            // 画面结束后仅保留房间拥有的声音尾段，不阻挡输入或延迟原结算。
            if (_age >= DeniaExecutionTimeline.AudioEnd) { QueueFree(); return; }
            if (_visualFinished) return;
            if (_video == null || _ink == null) return;
            // 抵消浮点插值误差，避免恰好在30 FPS切点时晚显示一帧。
            _frameClock = Clock + 0.00001f;
            if (!_videoStarted && ++_warmFrames >= 2) _video.Paused = true;
            // 首帧预解码后暂停；只提前一帧恢复以抵消解码调度，保留贴近镜头的开头。
            if (!_videoStarted && _frameClock >= DeniaExecutionTimeline.Enter - 1f / 30)
            {
                _videoStarted = true;
                _video.Paused = false;
            }
            bool videoPhase = _frameClock >= DeniaExecutionTimeline.Enter
                && _frameClock < DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video;
            _video.Visible = videoPhase;
            bool impact = _frameClock >= DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video && !_returning;
            if (impact)
            {
                _video.Paused = true;
                _poster.Texture ??= _video.GetVideoTexture();
                int tick = ImpactTick();
                _impactMaterial.SetShaderParameter("inverted", WhiteBeat(tick));
                _impactMaterial.SetShaderParameter("whiteout", tick >= 6);
            }
            _poster.Visible = impact;
            UpdateFrame();
            float ret = Mathf.Clamp((Clock - DeniaExecutionTimeline.Release) / DeniaExecutionTimeline.Return, 0, 1);
            float enter = Mathf.Clamp(Clock / 0.8f, 0, 1);
            _screen.Visible = _frameClock < DeniaExecutionTimeline.Enter || _returning;
            _screenMaterial.SetShaderParameter("center", _returning
                ? new Vector2(0.25f, 0.58f).Lerp(TargetUv, Smooth(ret)) : new Vector2(0.5f, 0.52f));
            _screenMaterial.SetShaderParameter("blur_amount", _returning
                ? 0.022f * (1 - ret) * (1 - ret) : 0.004f * enter);
            _screenMaterial.SetShaderParameter("wash", _returning ? 0.35f * (1 - ret) : 0.85f * Smooth(enter));
            _screenMaterial.SetShaderParameter("entrance_time", _returning ? -1f : Clock * 1.4f / DeniaExecutionTimeline.Enter);
            _screenMaterial.SetShaderParameter("zoom", _returning ? 1 + 0.08f * (1 - ret) : 1.04f - 0.04f * enter);
            _ink.QueueRedraw();
        }
        catch (Exception ex) { Abort(ex); }
    }

    private void UpdateFrame()
    {
        _size = GetViewport().GetVisibleRect().Size;
        _inputBlocker.Size = _size;
        float scale = Mathf.Min(_size.X / Design.X, _size.Y / Design.Y);
        _frame = new Rect2((_size - Design * scale) / 2, Design * scale);
        _screen.Size = _size;
        _video.Position = _poster.Position = _frame.Position;
        _video.Size = _poster.Size = _frame.Size;
        _ink.Size = _size;
    }

    private void Abort(Exception exception)
    {
        GD.PrintErr($"[DeniaExecution] Visual skipped: {exception}");
        QueueFree();
    }

    public override void _ExitTree()
    {
        _exit.Cancel();
        _machine?.Dispose();
        _backend?.Stop();
        _backend?.Dispose();
        if (GodotObject.IsInstanceValid(_video)) _video.Stop();
        if (GodotObject.IsInstanceValid(_audio)) _audio.Stop();
        _exit.Dispose();
    }

    internal void DrawFrame(Control canvas)
    {
        float scale = _frame.Size.X / Design.X;
        canvas.DrawSetTransform(_frame.Position, 0, new Vector2(scale, scale));
        if (!_returning && _frameClock >= DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video) DrawImpact(canvas);
        else if (_returning)
        {
            float t = (_frameClock - DeniaExecutionTimeline.Release) / DeniaExecutionTimeline.Return;
            float flash = 1 - Smooth(t / 0.45f);
            if (flash > 0) canvas.DrawRect(new Rect2(Vector2.Zero, Design), new Color(1, 1, 1, flash));
        }
        canvas.DrawSetTransform(Vector2.Zero);
        // 始终保持电影画幅，入场滤镜在切点直接交给原视频。
        DrawBars(canvas);
    }

    private void DrawBars(Control canvas)
    {
        float bars = _frameClock < 0.8f ? Smooth(_frameClock / 0.8f)
            : _returning ? 1 - Smooth((_frameClock - DeniaExecutionTimeline.Release) / DeniaExecutionTimeline.Return) : 1;
        if (_frame.Position.Y > 0)
        {
            canvas.DrawRect(new Rect2(0, 0, _size.X, _frame.Position.Y * bars), Colors.Black);
            canvas.DrawRect(new Rect2(0, _size.Y - _frame.Position.Y * bars, _size.X, _frame.Position.Y * bars), Colors.Black);
        }
        if (_frame.Position.X > 0)
        {
            canvas.DrawRect(new Rect2(0, 0, _frame.Position.X * bars, _size.Y), Colors.Black);
            canvas.DrawRect(new Rect2(_size.X - _frame.Position.X * bars, 0, _frame.Position.X * bars, _size.Y), Colors.Black);
        }
    }

    private int ImpactTick() => Mathf.Clamp((int)((_frameClock - DeniaExecutionTimeline.Enter - DeniaExecutionTimeline.Video) * 30), 0, 7);
    private static bool WhiteBeat(int tick) => tick is 0 or 4 or >= 6;

    private void DrawImpact(Control canvas)
    {
        int tick = ImpactTick();
        if (tick >= 6) { canvas.DrawRect(new Rect2(Vector2.Zero, Design), Colors.White); return; }
        bool white = WhiteBeat(tick);
        Color ink = white ? Colors.Black : Colors.White;
        var center = new Vector2(480, 514);
        for (int i = 0; i < 52; i++)
        {
            float a = i * Mathf.Tau / 52;
            float jitter = Hash(i + tick * 3);
            var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var side = direction.Orthogonal();
            float inner = 230 + jitter * 480;
            float outer = 1100 + jitter * 1100;
            canvas.DrawColoredPolygon(new[] { center + direction * inner,
                center + direction * outer - side * (8 + jitter * 24),
                center + direction * outer + side * (8 + jitter * 24) }, ink);
        }
        var burst = new Vector2[48];
        for (int i = 0; i < burst.Length; i++)
        {
            float a = i * Mathf.Tau / burst.Length;
            float radius = (i % 2 == 0 ? 160 : 55) * (0.7f + Hash(i + tick) * 0.8f);
            burst[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        canvas.DrawColoredPolygon(burst, ink);
        for (int i = 0; i < 18; i++)
        {
            float a = i * 2.39996f + tick * 0.2f;
            var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (150 + Hash(i) * 130);
            canvas.DrawCircle(p, 2 + Hash(i + 70) * 11, ink);
        }
    }

    private static float Hash(int i) => Mathf.PosMod(MathF.Sin(i * 127.1f + 311.7f) * 43758.5453f, 1);
    private static float Smooth(float t) { t = Mathf.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
}

internal partial class DeniaExecutionInk : Control
{
    internal DeniaExecutionOverlay Director { get; set; } = null!;
    public override void _Draw()
    {
        try { Director.DrawFrame(this); }
        catch (Exception ex) { GD.PrintErr($"[DeniaExecution] Draw skipped: {ex.Message}"); Director.QueueFree(); }
    }
}
