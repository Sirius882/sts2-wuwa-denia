using System;
using System.IO;
using System.Threading.Tasks;
using Denia;
using Godot;

public partial class Preview
{
    private async Task CheckMapBrush()
    {
        _viewport = new SubViewport { Size = new Vector2I(960, 720), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_viewport);
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var brush = new DeniaMapBrushVisual();_viewport.AddChild(brush);
            brush.Advance(0, false, Vector2.Zero, 1);
            Check(brush.GetChildCount() == 0, "no selected/down brush means no emission");
            brush.Advance(0, true, new Vector2(50, 60), 1);
            var burst = brush.GetChild<DeniaBubbleBurst>(0);burst.SetProcess(false);
            Check(!burst.IsBlack && burst.Position == new Vector2(50, 60), "brush uses pink click bubbles at pointer");
            for (int i = 0; i < fps; i++) burst._Process(1.0 / fps);
            Check(Math.Abs(burst.Elapsed - 0.55) < 1e-6, "map bubbles run slowly at " + fps + " FPS");
            for (int i = 0; i < fps * 2; i++) brush.Advance(1.0 / fps, true, new Vector2(100, 100), 1);
            Check(brush.GetChildCount() is >= 7 and <= 8, "stationary held brush emits slowly and continuously");
            int before = brush.GetChildCount();
            brush.Advance(10, false, Vector2.Zero, 1);
            Check(brush.GetChildCount() == before, "releasing brush/erasing cannot emit");
            brush.Advance(0, true, Vector2.Zero, 1);
            Check(brush.GetChildCount() == before + 1, "next pen down emits immediately");
            brush.Advance(10, true, Vector2.Zero, 1);
            Check(brush.GetChildCount() == before + 2, "lag does not accumulate a large emission burst");
            brush.Free();
        }
        var line = new Line2D { Points = [new(70, 200), new(200, 125), new(330, 230), new(475, 140), new(610, 235), new(760, 165), new(880, 225)],
            Width = 11, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
            JointMode = Line2D.LineJointMode.Round };
        DeniaMapBrushVisual.Style(line);_viewport.AddChild(line);
        Check(line.Texture == null && line.DefaultColor == Colors.White && line.Antialiased, "native colored stroke has no tinting texture");
        Check(line.Gradient.Sample(0).IsEqualApprox(new Color("ffe0f4")) && line.Gradient.Sample(1).IsEqualApprox(new Color("522b91")),
            "stroke starts light pink and ends deep purple with no default black/white endpoints");
        Check(line.Gradient.GetPointCount() == 5, "pink/lilac/purple stops preserve character palette");
        var visual = new DeniaMapBrushVisual();_viewport.AddChild(visual);
        if (_render)
        {
            for (int frame = 0; frame < 180; frame++)
            {
                float t = frame / 60f;
                Vector2 point = new(120 + 210 * t, 450 + Mathf.Sin(t * 2.8f) * 50);
                foreach (Node child in visual.GetChildren())
                    if (child is DeniaBubbleBurst bubble) bubble._Process(1.0 / 60);
                visual.Advance(1.0 / 60, frame < 120, point, 1);
                foreach (Node child in visual.GetChildren()) child.SetProcess(false);
                await Capture($"map_brush_{frame:D4}");
            }
        }
        line.Free();visual.Free();
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"map_brush\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} map brush checks, render={_render}");
    }
}
