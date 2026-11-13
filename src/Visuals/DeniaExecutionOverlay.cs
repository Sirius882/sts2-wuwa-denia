using System;
using System.Threading;
using Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace Denia;

/// <summary>房间拥有的全屏演出：代码几何入场／冲击 + Theora人物中段。</summary>
public partial class DeniaExecutionOverlay : Control
{
    private static readonly Vector2 Design = new(1920, 886);
    private static readonly Color Blue = new(0.24f, 0.53f, 1f);
    private static readonly Color Pink = new(1f, 0.25f, 0.69f);
    private static readonly Vector2[] Cookie = BuildCookie();
    private static readonly ArrayMesh CookieMesh = BuildCookieMesh();
    private readonly CancellationTokenSource _exit = new();
    private AnimationPlayer _player = null!;
    private VideoStreamPlayer _video = null!;
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
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            _screenMaterial = new ShaderMaterial {
                Shader = GD.Load<Shader>("res://shaders/denia_execution_screen.gdshader") };
            _screen = new ColorRect { Material = _screenMaterial, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_screen);
            _video = new VideoStreamPlayer {
                Name = "VideoStreamPlayer",
                ProcessPriority = 100,
                Stream = GD.Load<VideoStream>(DeniaExecutionTimeline.VideoPath),
                Expand = true, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
            if (_video.Stream == null) throw new InvalidOperationException("Execution video is missing");
            AddChild(_video);
            // 提前解码少量首帧，始终藏在代码转场后；进入人物段时从暂停处继续。
            _video.Play();
            _impactMaterial = new ShaderMaterial {
                Shader = GD.Load<Shader>("res://shaders/denia_execution_impact.gdshader") };
            _poster = new TextureRect { Material = _impactMaterial, Visible = false,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_poster);
            _ink = new DeniaExecutionInk { Director = this, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_ink);
            _player = new AnimationPlayer { Name = "AnimationPlayer", ProcessPriority = -100 };
            AddChild(_player);
            var library = new AnimationLibrary();
            AddClip(library, "Execution", 0, DeniaExecutionTimeline.Release);
            AddClip(library, "Return", DeniaExecutionTimeline.Release, DeniaExecutionTimeline.Return);
            _player.AddAnimationLibrary("", library);
            _backend = new GodotAnimationPlayerBackend(_player);
            _backend.Completed += id => { if (id == "Return") QueueFree(); };
            var builder = ModAnimStateMachineBuilder.Create();
            // 入场／人物／冲击共用一条时钟，避免逐段切换累计一帧的误差。
            builder.AddState("Execution");
            // 冲击末白帧等待游戏命令的固定时长，解码／状态机回调不驱动伤害。
            builder.AddState("Return");
            builder.AddAnyState("Release", "Return");
            _machine = builder.Build(_backend);
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
            if (_age > DeniaExecutionTimeline.Total + 2) { QueueFree(); return; }
            if (_video == null || _ink == null) return;
            _frameClock = Clock;
            if (!_videoStarted && ++_warmFrames >= 2) _video.Paused = true;
            // 黑场尾部预滚0.2秒，覆盖Theora的帧调度延迟；人物显现前仍由代码层遮挡。
            if (!_videoStarted && Clock >= DeniaExecutionTimeline.Enter - 0.2f)
            {
                _videoStarted = true;
                _video.Paused = false;
            }
            bool videoPhase = Clock >= DeniaExecutionTimeline.Enter
                && Clock < DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video;
            _video.Visible = videoPhase;
            bool impact = Clock >= DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video && !_returning;
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
            _screen.Visible = Clock < 1.2f || _returning;
            _screenMaterial.SetShaderParameter("center", _returning
                ? new Vector2(0.25f, 0.58f).Lerp(TargetUv, Smooth(ret)) : new Vector2(0.5f, 0.52f));
            _screenMaterial.SetShaderParameter("blur_amount", _returning
                ? 0.022f * (1 - ret) * (1 - ret) : 0.004f * enter);
            _screenMaterial.SetShaderParameter("wash", _returning ? 0.35f * (1 - ret) : 0.85f * Smooth(enter));
            _screenMaterial.SetShaderParameter("zoom", _returning ? 1 + 0.08f * (1 - ret) : 1.04f - 0.04f * enter);
            _ink.QueueRedraw();
        }
        catch (Exception ex) { Abort(ex); }
    }

    private void UpdateFrame()
    {
        Size = GetViewportRect().Size;
        float scale = Mathf.Min(Size.X / Design.X, Size.Y / Design.Y);
        _frame = new Rect2((Size - Design * scale) / 2, Design * scale);
        _screen.Size = Size;
        _video.Position = _poster.Position = _frame.Position;
        _video.Size = _poster.Size = _frame.Size;
        _ink.Size = Size;
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
        _exit.Dispose();
    }

    internal void DrawFrame(Control canvas)
    {
        float scale = _frame.Size.X / Design.X;
        canvas.DrawSetTransform(_frame.Position, 0, new Vector2(scale, scale));
        if (_frameClock < DeniaExecutionTimeline.Enter) DrawEnter(canvas);
        else if (_frameClock < DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video)
        {
            float alpha = 1 - Smooth((_frameClock - DeniaExecutionTimeline.Enter) / 0.12f);
            if (alpha > 0) canvas.DrawRect(new Rect2(Vector2.Zero, Design), new Color(0, 0, 0, alpha));
        }
        else if (!_returning) DrawImpact(canvas);
        else
        {
            float t = (_frameClock - DeniaExecutionTimeline.Release) / DeniaExecutionTimeline.Return;
            float flash = 1 - Smooth(t / 0.45f);
            if (flash > 0) canvas.DrawRect(new Rect2(Vector2.Zero, Design), new Color(1, 1, 1, flash));
        }
        canvas.DrawSetTransform(Vector2.Zero);
        // 黑边最后绘制，近景姜饼人和冲击线不能越过电影画幅。
        float bars = _frameClock < 0.8f ? Smooth(_frameClock / 0.8f)
            : _returning ? 1 - Smooth((_frameClock - DeniaExecutionTimeline.Release) / DeniaExecutionTimeline.Return) : 1;
        if (_frame.Position.Y > 0)
        {
            canvas.DrawRect(new Rect2(0, 0, Size.X, _frame.Position.Y * bars), Colors.Black);
            canvas.DrawRect(new Rect2(0, Size.Y - _frame.Position.Y * bars, Size.X, _frame.Position.Y * bars), Colors.Black);
        }
        if (_frame.Position.X > 0)
        {
            canvas.DrawRect(new Rect2(0, 0, _frame.Position.X * bars, Size.Y), Colors.Black);
            canvas.DrawRect(new Rect2(Size.X - _frame.Position.X * bars, 0, _frame.Position.X * bars, Size.Y), Colors.Black);
        }
    }

    private void DrawEnter(Control canvas)
    {
        float darkness = Smooth((_frameClock - 0.75f) / 0.38f);
        canvas.DrawRect(new Rect2(Vector2.Zero, Design), new Color(0.007f, 0.002f, 0.025f, darkness));
        for (int i = 0; i < 14; i++)
        {
            float k = i / 14f;
            float a = k * Mathf.Tau + _frameClock * 0.7f;
            var p = new Vector2(960 + Mathf.Cos(a) * (420 + 220 * k), 443 + Mathf.Sin(a) * 240);
            float fade = MathF.Sin(Mathf.Clamp(_frameClock / 1.8f, 0, 1) * Mathf.Pi) * 0.7f;
            DrawCookie(canvas, p, 8 + 19 * k, a - 1, new Vector2(1, 1), fade, 0.7f);
        }
        if (_frameClock < 0.76f) return;
        float q = Mathf.Clamp((_frameClock - 0.76f) / 1.14f, 0, 1);
        float size = 1900 * MathF.Pow(1 - q, 3.2f) + 2;
        var position = new Vector2(1370, 420).Lerp(new Vector2(850, 390), Smooth(q));
        float opacity = 1 - Smooth((q - 0.78f) / 0.22f);
        DrawCookie(canvas, position, size, -0.9f + q * 1.6f,
            new Vector2(1.35f - 0.5f * q, 0.35f + 0.65f * Smooth(q)), opacity, Smooth((q - 0.2f) / 0.35f));
    }

    private static void DrawCookie(Control canvas, Vector2 position, float size, float rotation,
        Vector2 perspective, float opacity, float dark)
    {
        if (opacity <= 0.001f || size < 1) return;
        var points = new Vector2[Cookie.Length];
        for (int i = 0; i < Cookie.Length; i++)
            points[i] = position + (Cookie[i] * perspective).Rotated(rotation) * size;
        var fill = new Color(0.96f, 0.88f, 1f).Lerp(new Color(0.035f, 0.009f, 0.12f), dark);
        fill.A = opacity;
        canvas.DrawMesh(CookieMesh, null, new Transform2D(rotation, perspective * size, 0, position), fill);
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0); closed[^1] = closed[0];
        for (int pass = 3; pass >= 0; pass--)
        {
            float width = Mathf.Max(1, size * 0.018f) * (1 + pass * 1.5f);
            var color = Blue.Lerp(Pink, 0.5f);
            color.A = opacity * (pass == 0 ? 0.95f : 0.055f);
            canvas.DrawPolyline(closed, color, width, true);
        }
        for (int i = 0; i < points.Length; i++)
        {
            Color color = Blue.Lerp(Pink, Mathf.Clamp((Cookie[i].X + 0.8f) / 1.6f, 0, 1));
            color.A = opacity * 0.95f;
            canvas.DrawLine(points[i], points[(i + 1) % points.Length], color, Mathf.Max(1, size * 0.007f), true);
        }
        var star = new Vector2[8];
        for (int i = 0; i < star.Length; i++)
        {
            float radius = i % 2 == 0 ? 0.18f : 0.035f;
            star[i] = position + (new Vector2(Mathf.Cos(i * Mathf.Pi / 4), Mathf.Sin(i * Mathf.Pi / 4))
                * radius * perspective).Rotated(rotation) * size;
        }
        canvas.DrawColoredPolygon(star, new Color(1f, 0.55f, 0.88f, opacity));
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

    private static Vector2[] BuildCookie()
    {
        Vector2[] anchors = [new(0, -0.96f), new(0.24f, -0.86f), new(0.28f, -0.64f),
            new(0.17f, -0.45f), new(0.58f, -0.55f), new(0.78f, -0.46f), new(0.72f, -0.29f),
            new(0.34f, -0.21f), new(0.29f, 0.04f), new(0.53f, 0.54f), new(0.44f, 0.73f),
            new(0.26f, 0.72f), new(0, 0.34f), new(-0.26f, 0.72f), new(-0.44f, 0.73f),
            new(-0.53f, 0.54f), new(-0.29f, 0.04f), new(-0.34f, -0.21f), new(-0.72f, -0.29f),
            new(-0.78f, -0.46f), new(-0.58f, -0.55f), new(-0.17f, -0.45f),
            new(-0.28f, -0.64f), new(-0.24f, -0.86f)];
        var points = new Vector2[anchors.Length * 6];
        for (int i = 0; i < anchors.Length; i++)
        {
            Vector2 a = anchors[(i + anchors.Length - 1) % anchors.Length], b = anchors[i],
                c = anchors[(i + 1) % anchors.Length], d = anchors[(i + 2) % anchors.Length];
            for (int j = 0; j < 6; j++)
            {
                float t = j / 6f;
                points[i * 6 + j] = 0.5f * (2 * b + (-a + c) * t
                    + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
            }
        }
        return points;
    }

    private static ArrayMesh BuildCookieMesh()
    {
        var vertices = new Vector3[Cookie.Length];
        for (int i = 0; i < Cookie.Length; i++) vertices[i] = new Vector3(Cookie[i].X, Cookie[i].Y, 0);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = Geometry2D.TriangulatePolygon(Cookie);
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
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
