using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Denia;
using Godot;

public partial class Preview : Control
{
    private int _checks;
    private readonly string _output = ProjectSettings.GlobalizePath("res://captures");
    private bool _render;
    private readonly bool _audioCheck = Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--audio-check");

    public override async void _Ready()
    {
        try
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(Preview).Assembly)!;
            string binary = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug");
            context.Resolving += (_, name) => {
                string path = Path.Combine(binary, name.Name + ".dll");
                return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            _render = DisplayServer.GetName() != "headless" && !_audioCheck;
            if (_audioCheck) Check(AudioServer.GetDriverName() == "WASAPI", "real-time check uses native audio output");
            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, "SFX");
            Directory.CreateDirectory(_output);
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            for (int bias = 0; bias <= 5; bias++)
            {
                Check(DeniaExecutionTimeline.ShouldPlay(bias, false, false) == (bias >= 3), "bias gate");
                Check(!DeniaExecutionTimeline.ShouldPlay(bias, true, false), "dead gate");
                Check(!DeniaExecutionTimeline.ShouldPlay(bias, false, true), "immune gate");
            }
            Check(Math.Abs(DeniaExecutionTimeline.Video - 137f / 30) < 0.0001, "video frame timing includes original gingerbread");
            Check(Math.Abs(DeniaExecutionTimeline.Release - 169f / 30) < 0.0001, "damage release timing is unchanged");
            await Play();
            if (_render) await CheckFullScreenCoverage();
            var interrupted = new DeniaExecutionOverlay();
            AddChild(interrupted);
            var exit = interrupted.ExitToken;
            var interruptedAudio = interrupted.GetNode<AudioStreamPlayer>("ExecutionAudio");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RemoveChild(interrupted);
            interrupted.QueueFree();
            Check(exit.IsCancellationRequested, "leaving room cancels presentation wait");
            Check(!interruptedAudio.Playing, "leaving room stops execution audio immediately");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GetChildCount() == 0, "all presentation nodes released");
            if (_render) await CheckDreamShader();
            File.WriteAllText(Path.Combine(_output, "checks.json"), $"{{\"checks\":{_checks},\"render\":{_render.ToString().ToLowerInvariant()}}}");
            GD.Print($"PASS: {_checks} execution checks, render={_render}");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PrintErr(ex); GetTree().Quit(1); }
    }

    private async Task Play()
    {
        const string name = "normal";
        var overlay = new DeniaExecutionOverlay();
        AddChild(overlay);
        var animation = overlay.GetNode<AnimationPlayer>("AnimationPlayer");
        var audio = overlay.GetNode<AudioStreamPlayer>("ExecutionAudio");
        Check(audio.Playing && !overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer").Visible,
            "audio starts with the filter before the video becomes visible");
        Check(audio.PitchScale == 1 && audio.Bus == "SFX" && !audio.StreamPaused,
            "execution audio uses original speed and the SFX bus independently of video pre-roll");
        Check(Math.Abs(audio.Stream.GetLength() - DeniaExecutionTimeline.AudioEnd) < 0.0001,
            "soundtrack continues for three seconds after the video ends");
        Check(animation.HasAnimation("Execution") && animation.HasAnimation("Return"), "Ritsu backend clips available");
        Check(animation.SpeedScale == 1 && overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer").SpeedScale == 1,
            "code animation and video always play at original speed");
        float maxVideoPosition = 0;
        double maxAudioDrift = 0;
        double outputLatency = AudioServer.GetOutputLatency();
        int audioSamples = 0;
        bool released = false, videoSeen = false, impactSeen = false, handoffSeen = false, tailSeen = false;
        int frame = 0;
        string folder = Path.Combine(_output, name);
        Directory.CreateDirectory(folder);
        foreach (string oldFrame in Directory.EnumerateFiles(folder, "frame_*.png")) File.Delete(oldFrame);
        double start = Time.GetTicksMsec();
        while (GodotObject.IsInstanceValid(overlay) && overlay.IsInsideTree() && frame < 2000)
        {
            if (_render) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            else await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree()) break;
            float clock = overlay.Clock;
            if (_audioCheck && clock > 0.2f && clock < DeniaExecutionTimeline.Release)
            {
                maxAudioDrift = Math.Max(maxAudioDrift,
                    Math.Abs(audio.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix() - outputLatency - clock));
                audioSamples++;
            }
            videoSeen |= clock >= DeniaExecutionTimeline.Enter && clock < DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video;
            impactSeen |= clock >= DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video && clock < DeniaExecutionTimeline.Release;
            var video = overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer");
            if (!overlay.Visible && !tailSeen)
            {
                Check(overlay.GetNode<Control>("InputBlocker").MouseFilter == MouseFilterEnum.Ignore
                    && (!_audioCheck || audio.Playing) && !video.IsPlaying(),
                    "audio tail survives the visual return without blocking input or decoding video");
                tailSeen = true;
            }
            maxVideoPosition = Math.Max(maxVideoPosition, (float)video.StreamPosition);
            if (!released && clock >= DeniaExecutionTimeline.Release - 0.005f)
            {
                overlay.RevealGame();
                released = true;
            }
            if (_render)
            {
                var image = GetViewport().GetTexture().GetImage();
                if (image.SavePng(Path.Combine(folder, $"frame_{frame:D4}.png")) != Error.Ok)
                    throw new IOException("frame capture failed");
                if (!handoffSeen && video.Visible)
                {
                    var texture = video.GetVideoTexture();
                    Check(texture.GetWidth() == 3840 && texture.GetHeight() == 1772,
                        "native video texture retains the full 3840x1772 upscaled resolution");
                    Check(Math.Abs(clock - DeniaExecutionTimeline.Enter) < 1f / 30 + 0.0001f && video.StreamPosition <= 0.10,
                        "direct video handoff preserves the close-up instead of pre-rolling past it");
                    Color body = image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
                    Check(body.R > 0.60f && body.G > 0.55f && body.B > 0.60f,
                        "first visible video frame is the pale extreme close-up, not a black frame");
                    handoffSeen = true;
                }
            }
            frame++;
        }
        Check(released, $"{name} reached white flash");
        Check(videoSeen && impactSeen, $"{name} Ritsu state chain advanced");
        Check(maxVideoPosition >= DeniaExecutionTimeline.Video - 1f / 30 - 0.015f,
            $"{name} video decoded through character segment ({maxVideoPosition:F3})");
        Check(!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree(), $"{name} overlay completed");
        Check(tailSeen, "presentation retained its fading audio after returning to game");
        if (_render) Check(handoffSeen, "GPU captured the direct filter-to-video handoff");
        if (_audioCheck)
        {
            Check(audioSamples > 100 && maxAudioDrift < 0.08,
                $"native audio stays on the presentation clock ({maxAudioDrift:F4}s maximum drift)");
            File.WriteAllText(Path.Combine(_output, "audio_sync.json"),
                $"{{\"samples\":{audioSamples},\"maxDriftSeconds\":{maxAudioDrift.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"outputLatencySeconds\":{outputLatency.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        }
        GD.Print($"{name}: frames={frame}, video={maxVideoPosition:F3}s, elapsed={(Time.GetTicksMsec() - start) / 1000:F3}s");
    }

    private async Task CheckDreamShader()
    {
        var backdrop = new ColorRect { Size = GetViewportRect().Size, Color = new Color(0.12f, 0.12f, 0.12f) };
        var copy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/denia_execution_screen.gdshader") };
        var screen = new ColorRect { Size = backdrop.Size, Material = material };
        AddChild(backdrop); AddChild(copy); AddChild(screen);
        var samples = new Image[4];
        float[] times = [0, 0.45f, 0.85f, 1.4f];
        for (int i = 0; i < times.Length; i++)
        {
            material.SetShaderParameter("entrance_time", times[i]);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            samples[i] = GetViewport().GetTexture().GetImage();
            samples[i].SavePng(Path.Combine(_output, $"dream_{i}.png"));
        }
        float Center(Image image) => Brightness(image, new Vector2(0.5f, 0.5f));
        float Edge(Image image) => (Brightness(image, new Vector2(0.08f, 0.5f))
            + Brightness(image, new Vector2(0.5f, 0.12f)) + Brightness(image, new Vector2(0.92f, 0.5f))
            + Brightness(image, new Vector2(0.5f, 0.88f))) / 4;
        Check(Math.Abs(Center(samples[0]) - Edge(samples[0])) < 0.01f, "dream filter starts without an opaque tint");
        Check(Edge(samples[2]) > Center(samples[2]) + 0.10f, "dream opacity is stronger near edges while center stays translucent");
        float edgeGrowth = Edge(samples[2]) - Edge(samples[1]), centerGrowth = Center(samples[2]) - Center(samples[1]);
        Check(edgeGrowth > 0.05f && centerGrowth > 0 && Math.Abs(edgeGrowth - centerGrowth) > 0.03f,
            "dream filter grows over time at different rates across the screen");
        Check(Center(samples[3]) > 0.70f && Edge(samples[3]) > 0.70f,
            "pink-white filter becomes strong enough for the pale close-up handoff");
        backdrop.QueueFree(); copy.QueueFree(); screen.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckFullScreenCoverage()
    {
        var room = new Control { Position = new Vector2(133, 79), Size = new Vector2(640, 360),
            Scale = new Vector2(0.65f, 0.8f), ClipContents = true };
        AddChild(room);
        var hud = new CanvasLayer { Layer = 1 };
        var tips = new CanvasLayer { Layer = 10 };
        AddChild(hud); AddChild(tips);
        hud.AddChild(new ColorRect { Size = new Vector2(1280, 80), Color = Colors.Red });
        hud.AddChild(new ColorRect { Position = new Vector2(20, 96), Size = new Vector2(60, 60), Color = Colors.Magenta });
        tips.AddChild(new ColorRect { Position = new Vector2(1000, 140), Size = new Vector2(120, 100), Color = Colors.Cyan });
        var window = GetWindow();
        bool embedded = window.GuiEmbedSubwindows;
        window.GuiEmbedSubwindows = true;
        var popup = new Window { Position = new Vector2I(930, 280), Size = new Vector2I(180, 120),
            Borderless = true, Unfocusable = true };
        AddChild(popup);
        popup.AddChild(new ColorRect { Size = new Vector2(180, 120), Color = Colors.Lime });
        popup.Show();
        await DrawFrame();
        var baseline = GetViewport().GetTexture().GetImage();
        Vector2I[] points = [new(220, 40), new(40, 120), new(1040, 180), new(1010, 330)];
        Color[] markers = [Colors.Red, Colors.Magenta, Colors.Cyan, Colors.Lime];
        for (int i = 0; i < points.Length; i++)
            Check(Difference(baseline.GetPixelv(points[i]), markers[i]) < 0.01f, $"HUD/popup fixture {i} is visible before presentation");
        var overlay = new DeniaExecutionOverlay();
        room.AddChild(overlay);
        var exit = overlay.ExitToken;
        overlay.GetNode<AnimationPlayer>("AnimationPlayer").Pause();
        Check(overlay.Layer > 1024, "room-owned presentation renders above embedded windows and HUD canvases");
        var blocker = overlay.GetNode<Control>("InputBlocker");
        var video = overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer");
        foreach (float clock in new[] { 0.65f, DeniaExecutionTimeline.Enter - 1f / 30, 1.0f, DeniaExecutionTimeline.Release + 0.15f })
        {
            if (clock > DeniaExecutionTimeline.Release)
            {
                overlay.RevealGame();
                overlay.GetNode<AnimationPlayer>("AnimationPlayer").Pause();
            }
            overlay.Clock = clock;
            await DrawFrame();
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Path.Combine(_output, $"coverage_{clock:F2}.png"));
            for (int i = 0; i < points.Length; i++)
                Check(Difference(image.GetPixelv(points[i]), baseline.GetPixelv(points[i])) > 0.1f,
                    $"filter/video/return covers HUD and embedded popup {i} at {clock:F2}s");
            Check(blocker.GetGlobalTransformWithCanvas().Origin.IsEqualApprox(Vector2.Zero)
                && blocker.Size.IsEqualApprox(GetViewportRect().Size),
                "presentation ignores room offset, scale and clipping and blocks the full viewport");
            if (clock < DeniaExecutionTimeline.Enter)
            {
                foreach (var corner in new[] { Vector2I.Zero, new Vector2I(image.GetWidth() - 1, 0),
                    new Vector2I(0, image.GetHeight() - 1), new Vector2I(image.GetWidth() - 1, image.GetHeight() - 1) })
                    Check(Difference(image.GetPixelv(corner), Colors.Black) > 0.1f,
                        "entrance filter reaches every corner without cinematic bars");
                if (clock > 0.7f)
                {
                    image.Convert(Image.Format.Rgb8);
                    Check(Array.TrueForAll(image.GetData(), channel => channel >= 250),
                        "last code-animation frame is white across the entire viewport before the video cut");
                }
            }
            if (clock == 1)
                Check(Brightness(image, new Vector2(0.5f, 0.03f)) < 0.01f,
                    "opaque cinematic bars also cover the top HUD");
        }
        Vector2I oldSize = window.Size;
        foreach (var size in new[] { new Vector2I(1000, 800), new Vector2I(1600, 720) })
        {
            window.Size = size;
            await DrawFrame();
            var viewport = GetViewportRect().Size;
            Check(blocker.Size.IsEqualApprox(viewport) && blocker.GetGlobalTransformWithCanvas().Origin.IsEqualApprox(Vector2.Zero),
                "full-window coverage follows viewport resizing");
            Check(video.Position.DistanceTo((viewport - video.Size) / 2) < 0.001f
                && Math.Abs(video.Size.X / video.Size.Y - 1920f / 886) < 0.001f,
                $"resized video stays centered and preserves the source aspect ratio: viewport={viewport}, position={video.Position}, size={video.Size}");
        }
        window.Size = oldSize;
        room.QueueFree(); hud.QueueFree(); tips.QueueFree(); popup.QueueFree();
        await DrawFrame();
        window.GuiEmbedSubwindows = embedded;
        Check(exit.IsCancellationRequested, "room removal releases the high canvas and its audio");
    }

    private async Task DrawFrame()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private static float Difference(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    private static float Brightness(Image image, Vector2 uv)
    {
        float sum = 0;
        for (int x = -8; x <= 8; x += 4)
            for (int y = -8; y <= 8; y += 4)
            {
                Color c = image.GetPixel((int)(image.GetWidth() * uv.X) + x, (int)(image.GetHeight() * uv.Y) + y);
                sum += (c.R + c.G + c.B) / 3;
            }
        return sum / 25;
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.09f, 0.08f, 0.13f));
        for (int i = 0; i < 12; i++)
            DrawLine(new Vector2(0, size.Y * 0.42f + i * size.Y * 0.05f),
                new Vector2(size.X, size.Y * 0.42f + i * size.Y * 0.05f), new Color(0.14f, 0.12f, 0.19f), 2);
        DrawCircle(size * new Vector2(0.24f, 0.45f), size.Y * 0.12f, new Color(0.92f, 0.47f, 0.7f));
        DrawCircle(size * new Vector2(0.72f, 0.45f), size.Y * 0.14f, new Color(0.3f, 0.54f, 0.59f));
        for (int i = 0; i < 5; i++)
        {
            var rect = new Rect2(size.X * (0.22f + i * 0.115f), size.Y * 0.77f, size.X * 0.10f, size.Y * 0.21f);
            DrawRect(rect, new Color(0.35f + i * 0.035f, 0.22f, 0.33f));
            DrawRect(rect.Grow(-6), new Color(0.12f, 0.10f, 0.16f), false, 2);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(26, 40), "DENIA / RITSULIB - STANDALONE TRANSITION PREVIEW", fontSize: 22);
    }
}
