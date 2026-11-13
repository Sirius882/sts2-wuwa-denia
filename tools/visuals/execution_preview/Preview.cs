using System;
using System.IO;
using System.Reflection;
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
            var cookie = (Vector2[])typeof(DeniaExecutionOverlay).GetField("Cookie", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            Check(Geometry2D.TriangulatePolygon(cookie).Length == (cookie.Length - 2) * 3, "cookie is a simple triangulatable contour");
            CheckEntry(cookie);
            Check(Math.Abs(DeniaExecutionTimeline.Video - 104f / 30) < 0.0001, "video frame timing");
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
        bool released = false, videoSeen = false, impactSeen = false, whiteBodySeen = false, backFaceSeen = false;
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
                if (clock >= 1.22f && clock < 1.28f)
                {
                    Color body = image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
                    if (body.R >= 0.85f || body.B < 0.7f || body.B - body.R < 0.08f)
                        throw new InvalidOperationException("flip must render the lavender back without a front-facing chest star");
                    backFaceSeen = true;
                }
                if (clock >= 1.84f && clock < 1.89f)
                {
                    image.Convert(Image.Format.Rgb8);
                    foreach (byte channel in image.GetData())
                        if (channel < 250) throw new InvalidOperationException("incoming white body must cover every viewport pixel, including bars");
                    whiteBodySeen = true;
                }
            }
            frame++;
        }
        Check(released, $"{name} reached white flash");
        Check(videoSeen && impactSeen, $"{name} Ritsu state chain advanced");
        Check(maxVideoPosition >= DeniaExecutionTimeline.Video - 1f / 30 - 0.015f,
            $"{name} video decoded through character segment ({maxVideoPosition:F3})");
        Check(!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree(), $"{name} overlay completed");
        if (_render) Check(whiteBodySeen, "GPU captured a fully white body before the video cut");
        if (_render) Check(backFaceSeen, "GPU captured a visible back face during the flip");
        GD.Print($"{name}: frames={frame}, video={maxVideoPosition:F3}s, elapsed={(Time.GetTicksMsec() - start) / 1000:F3}s");
    }

    private void CheckEntry(Vector2[] cookie)
    {
        var first = DeniaExecutionOverlay.EntryPose(0.76f);
        var centered = DeniaExecutionOverlay.EntryPose(1.2f);
        var near = DeniaExecutionOverlay.EntryPose(1.8f);
        Check(first.Position.X + first.Size < 0 && centered.Position.IsEqualApprox(new Vector2(960, 443)),
            "main cookie flies from outside the left edge to the center");
        bool horizontal = true;
        float lowestRotation = 10, highestRotation = -10;
        for (float time = 0.76f; time <= 1.2f; time += 0.01f)
        {
            var pose = DeniaExecutionOverlay.EntryPose(time);
            Vector2 bodyAxis = Vector2.Up.Rotated(pose.Rotation);
            horizontal &= Math.Abs(bodyAxis.X) > Math.Abs(bodyAxis.Y) * 2;
            lowestRotation = Math.Min(lowestRotation, pose.Rotation);
            highestRotation = Math.Max(highestRotation, pose.Rotation);
        }
        Check(horizontal, "incoming cookie lies horizontally with its head pointing right");
        Check(highestRotation - lowestRotation > 0.4f, "horizontal flight banks along its arc");
        int faceChanges = 0;
        float previousProjection = DeniaExecutionOverlay.EntryPose(0.94f).Perspective.X;
        bool backSeen = false, thinSeen = false;
        for (float time = 0.95f; time <= 1.56f; time += 0.005f)
        {
            var pose = DeniaExecutionOverlay.EntryPose(time);
            if (pose.Perspective.X * previousProjection < 0) faceChanges++;
            backSeen |= pose.Perspective.X < -0.95f && pose.Position.X > 500;
            thinSeen |= Math.Abs(pose.Perspective.X) < 0.08f;
            previousProjection = pose.Perspective.X;
        }
        Check(faceChanges == 2 && backSeen && thinSeen && DeniaExecutionOverlay.EntryPose(1.56f).Perspective.X > 0.99f,
            "visible full flip passes front, edge, back, edge and front with signed orientation");
        bool approaching = true;
        float previousSize = centered.Size;
        for (float time = 1.21f; time <= 1.8f; time += 0.01f)
        {
            var pose = DeniaExecutionOverlay.EntryPose(time);
            approaching &= pose.Position.IsEqualApprox(centered.Position) && pose.Size > previousSize;
            previousSize = pose.Size;
        }
        Check(approaching && near.Size > centered.Size * 10, "cookie approaches camera after reaching center, without shrinking or drifting");
        var contour = new Vector2[cookie.Length];
        for (int i = 0; i < cookie.Length; i++)
            contour[i] = near.Position + (cookie[i] * near.Perspective).Rotated(near.Rotation) * near.Size;
        bool covered = true;
        foreach (float aspect in new[] { 16f / 9, 21f / 9, 4f / 3 })
        {
            Vector2 viewport = aspect >= 1920f / 886 ? new Vector2(886 * aspect, 886) : new Vector2(1920, 1920 / aspect);
            Vector2 topLeft = centered.Position - viewport / 2;
            for (int x = 0; x <= 20; x++)
                for (int y = 0; y <= 20; y++)
                    covered &= Geometry2D.IsPointInPolygon(topLeft + viewport * new Vector2(x / 20f, y / 20f), contour);
        }
        Check(covered, "the body geometry covers the full 16:9, 21:9 and 4:3 viewport before cutting");
    }

    private async Task CheckDreamShader()
    {
        var backdrop = new ColorRect { Size = GetViewportRect().Size, Color = new Color(0.12f, 0.12f, 0.12f) };
        var copy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/denia_execution_screen.gdshader") };
        var screen = new ColorRect { Size = backdrop.Size, Material = material };
        AddChild(backdrop); AddChild(copy); AddChild(screen);
        var samples = new Image[3];
        float[] times = [0, 0.55f, 1.2f];
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
