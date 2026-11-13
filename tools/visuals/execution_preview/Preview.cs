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
        bool released = false, videoSeen = false, impactSeen = false;
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
            if (_render && GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, $"frame_{frame:D4}.png")) != Error.Ok)
                throw new IOException("frame capture failed");
            frame++;
        }
        Check(released, $"{name} reached white flash");
        Check(videoSeen && impactSeen, $"{name} Ritsu state chain advanced");
        Check(maxVideoPosition >= DeniaExecutionTimeline.Video - 1f / 30 - 0.015f,
            $"{name} video decoded through character segment ({maxVideoPosition:F3})");
        Check(!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree(), $"{name} overlay completed");
        GD.Print($"{name}: frames={frame}, video={maxVideoPosition:F3}s, elapsed={(Time.GetTicksMsec() - start) / 1000:F3}s");
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
