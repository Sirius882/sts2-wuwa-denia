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
    private DeniaFlowIdleVisual _idle = null!;
    private AnimatedSprite2D _actions = null!;

    public override async void _Ready()
    {
        try
        {
            _render = DisplayServer.GetName() != "headless";
            bool black = OS.GetCmdlineUserArgs().Length > 0 && OS.GetCmdlineUserArgs()[0] == "black";
            Directory.CreateDirectory(_output);
            _viewport = new SubViewport { Size = new Vector2I(592, 600), TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(_viewport);
            var scene = GD.Load<PackedScene>("res://scenes/creature_visuals/denia_pink.tscn").Instantiate<Node2D>();
            _viewport.AddChild(scene);
            scene.GetNode<AnimatedSprite2D>("PinkVisuals").Visible = false;
            scene.GetNode<AnimatedSprite2D>("DarkVisuals").Visible = false;
            _actions = scene.GetNode<AnimatedSprite2D>(black ? "DarkVisuals" : "PinkVisuals");
            _idle = black ? new DeniaBlackIdleVisual() : new DeniaPinkIdleVisual();
            _idle.Position = _idle.DesignOrigin;
            _viewport.AddChild(_idle);
            _idle.SetProcess(false);
            Check(_idle.Initialized, "production resources initialize");
            _idle.BindActions(_actions);
            Check(_idle.Visible && !_actions.Visible, "realtime idle replaces old sprite");
            Check(_actions.SpriteFrames.GetFrameCount("idle") == 1, "old idle atlas has been removed from scene");
            _actions.AnimationFinished += () => {
                if (_actions.Animation == "attack" || _actions.Animation == "dodge") _actions.Play("idle");
            };
            foreach (string action in black ? new[] { "attack", "dodge" } : new[] { "attack", "dodge", "death" })
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
            Check(Math.Abs(_idle.Clock - (_idle.DurationSeconds - 0.25)) < 1e-8, "negative seek wraps");
            _idle.Seek(_idle.DurationSeconds);
            Check(_idle.Clock == 0, "loop duration wraps exactly");
            try { _idle.Seek(double.NaN); Check(false, "invalid clock rejected"); }
            catch (ArgumentOutOfRangeException) { Check(true, "invalid clock rejected"); }
            _idle.Hide();
            _idle._Process(1);
            Check(_idle.Clock == 0, "hidden form pauses work and clock");
            _idle.Show();
            DeniaFlowIdleVisual second = black ? new DeniaBlackIdleVisual() : new DeniaPinkIdleVisual();
            second.Visible = false;
            _viewport.AddChild(second);
            second.Seek(5);
            Check(_idle.Clock == 0 && Math.Abs(second.Clock - 5) < 1e-8, "creature clocks are independent");
            second.QueueFree();
            var material = (ShaderMaterial)_idle.GetNode<ColorRect>("FlowBody").Material;
            var frames = material.GetShaderParameter("frames").AsGodotObject() as Texture2D;
            Check(frames != null && frames.GetSize() == new Vector2(4736, black ? 3600 : 4200), "all frame tiles resident");
            var flow = material.GetShaderParameter("flow_fields").AsGodotObject() as Texture2D;
            Check(flow != null && flow.GetSize() == (black ? new Vector2(328, 402) : new Vector2(536, 483)), "flow atlas dimensions");
            _idle.Seek(_idle.DurationSeconds - 0.001);
            Check(material.GetShaderParameter("frame_index").AsInt32() == (black ? 47 : 54), "last interval selects correct frame");
            if (black)
            {
                Check(material.GetShaderParameter("fraction").AsSingle() > 0.98f, "last black frame interpolates back to first");
                var before = material.GetShaderParameter("halo").AsVector2();
                _idle.Seek(0);
                Check(before.DistanceTo(material.GetShaderParameter("halo").AsVector2()) < 0.02f, "tracked halo continuous across loop");
            }
            _idle.Seek(0);
            var hiddenParent = new Node2D { Visible = false };
            _viewport.AddChild(hiddenParent);
            _idle.Reparent(hiddenParent);
            _idle._Process(1);
            Check(_idle.Clock == 0, "parent form visibility pauses hidden idle");
            _idle.Reparent(_viewport);
            hiddenParent.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(_actions.SpeedScale == 1 && !_actions.IsPlaying(), "old idle is paused without changing action speed");
            if (_render)
            {
                for (int i = 0; i < _idle.FrameCount; i++)
                {
                    _idle.Seek(i / (double)(black ? 48 : 55) * _idle.DurationSeconds + (!black && i == 55 ? -0.000001 : 0));
                    await Capture($"key_{i:D2}");
                }
                _idle.Seek(black ? 2.125 : 4.125);
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
                _idle.Position = _idle.DesignOrigin;
                _actions.Play("idle");
                // Isolate the real production shader for comparison against the supplied GLSL math.
                var body = _idle.GetNode<ColorRect>("FlowBody");
                body.Reparent(_viewport, false);
                body.Position = Vector2.Zero;
                _idle.Hide();
                foreach (double seconds in black ? new[] { 0, 2.125, 5.315 } : new[] { 0, 4.125, 10.631 })
                {
                    _idle.Seek(seconds);
                    await Capture($"pure_{seconds:F3}");
                }
                body.Reparent(_idle, false);
                body.Position = -_idle.DesignOrigin;
                _idle.Show();
                // Capture a complete 60 Hz loop, including the wrap, from the production renderer.
                for (int i = 0; i <= (int)Math.Ceiling(_idle.DurationSeconds * 60) + 2; i++)
                {
                    _idle.Seek(i / 60.0);
                    await Capture($"loop_{i:D4}");
                }
            }
            File.WriteAllText(Path.Combine(_output, "checks.json"),
                $"{{\"checks\":{_checks},\"form\":\"{(black ? "black" : "pink")}\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
            GD.Print($"PASS: {_checks} idle checks, black={black}, render={_render}");
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
