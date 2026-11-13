using System;
using System.IO;
using System.Threading.Tasks;
using Denia;
using Godot;

public partial class Preview : Node
{
    private int _checks;
    private bool _render;
    private readonly string _output = ProjectSettings.GlobalizePath("res://captures");
    private SubViewport _viewport = null!;
    private DeniaPinkIdleVisual _idle = null!;
    private AnimatedSprite2D _actions = null!;

    public override async void _Ready()
    {
        try
        {
            _render = DisplayServer.GetName() != "headless";
            Directory.CreateDirectory(_output);
            _viewport = new SubViewport { Size = new Vector2I(592, 600), TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(_viewport);
            var scene = GD.Load<PackedScene>("res://scenes/creature_visuals/denia_pink.tscn").Instantiate<Node2D>();
            _viewport.AddChild(scene);
            _actions = scene.GetNode<AnimatedSprite2D>("PinkVisuals");
            _actions.Visible = false;
            scene.GetNode<AnimatedSprite2D>("DarkVisuals").Visible = false;
            _idle = new DeniaPinkIdleVisual { Position = DeniaPinkIdleVisual.Origin };
            _viewport.AddChild(_idle);
            _idle.SetProcess(false);
            Check(_idle.Initialized, "production resources initialize");
            _idle.BindActions(_actions);
            Check(_idle.Visible && !_actions.Visible, "realtime idle replaces old sprite");
            Check(_actions.SpriteFrames.GetFrameCount("idle") == 1, "old idle atlas has been removed from scene");
            _actions.AnimationFinished += () => {
                if (_actions.Animation == "attack" || _actions.Animation == "dodge") _actions.Play("idle");
            };
            foreach (string action in new[] { "attack", "dodge", "death" })
            {
                Check(_actions.SpriteFrames.HasAnimation(action), action + " retained");
                _actions.Play(action);
                Check(!_idle.Visible && _actions.Visible, action + " immediately hides realtime idle");
                if (action != "death")
                {
                    _actions.EmitSignal(AnimatedSprite2D.SignalName.AnimationFinished);
                    Check(_idle.Visible && !_actions.Visible, action + " finish restores realtime idle");
                }
                else
                {
                    _actions.Pause();
                    _actions.SetFrameAndProgress(_actions.SpriteFrames.GetFrameCount("death") - 1, 1);
                    Check(!_idle.Visible && _actions.Visible, "death terminal pose stays visible");
                }
                _actions.Play("idle");
            }
            foreach (int fps in new[] { 30, 60, 144 })
            {
                _idle.Seek(0);
                for (int frame = 0; frame < fps * 2; frame++) _idle._Process(1.0 / fps);
                Check(Math.Abs(_idle.Clock - 2) < 1e-8, fps + " FPS preserves elapsed time");
            }
            _idle.Seek(-0.25);
            Check(Math.Abs(_idle.Clock - (DeniaPinkIdleVisual.Duration - 0.25)) < 1e-8, "negative seek wraps");
            _idle.Seek(DeniaPinkIdleVisual.Duration);
            Check(_idle.Clock == 0, "loop duration wraps exactly");
            try { _idle.Seek(double.NaN); Check(false, "invalid clock rejected"); }
            catch (ArgumentOutOfRangeException) { Check(true, "invalid clock rejected"); }
            _idle.Hide();
            _idle._Process(1);
            Check(_idle.Clock == 0, "hidden form pauses work and clock");
            _idle.Show();
            var second = new DeniaPinkIdleVisual { Visible = false };
            _viewport.AddChild(second);
            second.Seek(5);
            Check(_idle.Clock == 0 && second.Clock == 5, "creature clocks are independent");
            second.QueueFree();
            var material = (ShaderMaterial)_idle.GetNode<ColorRect>("FlowBody").Material;
            var frames = material.GetShaderParameter("frames").AsGodotObject() as Texture2D;
            Check(frames != null && frames.GetSize() == new Vector2(4736, 4200), "all 56 frame tiles resident");
            var flow = material.GetShaderParameter("flow_fields").AsGodotObject() as Texture2D;
            Check(flow != null && flow.GetSize() == new Vector2(536, 483), "flow atlas dimensions");
            if (_render)
            {
                for (int i = 0; i < 56; i++)
                {
                    _idle.Seek(i / 55.0 * DeniaPinkIdleVisual.Duration + (i == 55 ? -0.000001 : 0));
                    await Capture($"key_{i:D2}");
                }
                _idle.Seek(4.125);
                await Capture("interpolated");
                _idle.SetFacing(true);
                await Capture("flipped");
                _idle.SetFacing(false);
                _idle.Modulate = new Color(1, 1, 1, 0.5f);
                await Capture("half_alpha");
                _idle.Modulate = Colors.White;
                _idle.Scale = Vector2.One * 0.5f;
                _idle.Position = new Vector2(296, 448);
                await Capture("game_size");
                _idle.Scale = Vector2.One;
                _idle.Position = DeniaPinkIdleVisual.Origin;
                _actions.Play("idle");
                // Isolate the real production shader for comparison against the supplied GLSL math.
                var body = _idle.GetNode<ColorRect>("FlowBody");
                body.Reparent(_viewport, false);
                body.Position = Vector2.Zero;
                _idle.Hide();
                foreach (double seconds in new[] { 0, 4.125, 10.631 })
                {
                    _idle.Seek(seconds);
                    await Capture($"pure_{seconds:F3}");
                }
                body.Reparent(_idle, false);
                body.Position = -DeniaPinkIdleVisual.Origin;
                _idle.Show();
                // Capture a complete 60 Hz loop, including the wrap, from the production renderer.
                for (int i = 0; i <= 640; i++)
                {
                    _idle.Seek(i / 60.0);
                    await Capture($"loop_{i:D4}");
                }
            }
            File.WriteAllText(Path.Combine(_output, "checks.json"),
                $"{{\"checks\":{_checks},\"render\":{_render.ToString().ToLowerInvariant()}}}");
            GD.Print($"PASS: {_checks} pink idle checks, render={_render}");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PrintErr(ex); GetTree().Quit(1); }
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = _viewport.GetTexture().GetImage();
        Check(image.SavePng(Path.Combine(_output, name + ".png")) == Error.Ok, "GPU capture " + name);
        Check(image.GetPixel(0, 0).A < 0.01f && image.GetPixel(591, 599).A < 0.01f,
            "transparent corners " + name);
    }

    private void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        _checks++;
    }
}
