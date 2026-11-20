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
            Check(brush.ActiveParticleCount == 0, "inactive brush starts empty");
            brush.Advance(0, true, new Vector2(50, 60), 1);
            Check(brush.ActiveParticleCount == 1, "pen down emits one particle immediately");
            int previous = brush.ActiveParticleCount;
            for (int i = 0; i < fps; i++)
            {
                brush.Advance(1.0 / fps, true, new Vector2(50, 60), 1);
                int current = brush.ActiveParticleCount;
                Check(current - previous is 0 or 1, "only one particle can be emitted on a frame");
                previous = current;
            }
            Check(brush.ActiveParticleCount is 6 or 7, "stationary held brush emits six particles per second at " + fps + " FPS");
            int before = brush.ActiveParticleCount;
            brush.Advance(0.1, false, Vector2.Zero, 1);
            Check(brush.ActiveParticleCount == before, "pen up stops emission and leaves particles to fade");
            brush.Advance(0, true, Vector2.Zero, 1);
            Check(brush.ActiveParticleCount == before + 1, "next pen down emits immediately");
            brush.Advance(10, true, Vector2.Zero, 1);
            Check(brush.ActiveParticleCount == 1, "lag ages old particles and emits only one new particle");
            brush.Advance(3, false, Vector2.Zero, 1);
            Check(brush.ActiveParticleCount == 0 && brush.GetChildCount() == 0, "particles fully fade without child node allocations");
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
                visual.Advance(1.0 / 60, frame < 120, point, 1);
                await Capture($"map_brush_{frame:D4}");
            }
        }
        line.Free();visual.Free();
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"map_brush\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} map brush checks, render={_render}");
    }
}
