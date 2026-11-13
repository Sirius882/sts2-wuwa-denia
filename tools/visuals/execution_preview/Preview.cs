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
            _render = DisplayServer.GetName() != "headless";
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
            var interrupted = new DeniaExecutionOverlay();
            AddChild(interrupted);
            var exit = interrupted.ExitToken;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RemoveChild(interrupted);
            interrupted.QueueFree();
            Check(exit.IsCancellationRequested, "leaving room cancels presentation wait");
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
        Check(animation.HasAnimation("Execution") && animation.HasAnimation("Return"), "Ritsu backend clips available");
        Check(animation.SpeedScale == 1 && overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer").SpeedScale == 1,
            "code animation and video always play at original speed");
        float maxVideoPosition = 0;
        bool released = false, videoSeen = false, impactSeen = false, handoffSeen = false;
        int frame = 0;
        string folder = Path.Combine(_output, name);
        Directory.CreateDirectory(folder);
        foreach (string oldFrame in Directory.EnumerateFiles(folder, "frame_*.png")) File.Delete(oldFrame);
        double start = Time.GetTicksMsec();
        while (GodotObject.IsInstanceValid(overlay) && overlay.IsInsideTree() && frame < 280)
        {
            if (_render) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            else await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree()) break;
            float clock = overlay.Clock;
            videoSeen |= clock >= DeniaExecutionTimeline.Enter && clock < DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video;
            impactSeen |= clock >= DeniaExecutionTimeline.Enter + DeniaExecutionTimeline.Video && clock < DeniaExecutionTimeline.Release;
            var video = overlay.GetNode<VideoStreamPlayer>("VideoStreamPlayer");
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
        if (_render) Check(handoffSeen, "GPU captured the direct filter-to-video handoff");
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
