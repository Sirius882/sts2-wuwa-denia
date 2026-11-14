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
            if (OS.GetCmdlineUserArgs().Length > 0 && OS.GetCmdlineUserArgs()[0] == "pink_1hit")
            {
                await CheckPinkAttack();
                GetTree().Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Length > 0 && OS.GetCmdlineUserArgs()[0] == "black_rise")
            {
                await CheckRise();
                GetTree().Quit();
                return;
            }
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
            Check(material.GetShaderParameter("frame_index").AsInt32() == (black ? 47 : 55), "last interval selects correct frame");
            Check(material.GetShaderParameter("fraction").AsSingle() > 0.98f, "last frame interpolates back to first");
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
            CheckBlankUi();
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var burst = new DeniaBubbleBurst { IsBlack = black };
                _viewport.AddChild(burst);
                burst.SetProcess(false);
                for (int f = 0; f < fps; f++) burst._Process(1.0 / fps);
                Check(Math.Abs(burst.Elapsed - 1) < 1e-8, "bubble speed uses elapsed time at " + fps + " FPS");
                burst._Process(0.6);
                Check(burst.IsQueuedForDeletion(), "bubble cleans itself after lifetime");
            }
            if (_render)
            {
                for (int i = 0; i < _idle.FrameCount; i++)
                {
                    _idle.Seek(i / (double)_idle.FrameCount * _idle.DurationSeconds);
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
                if (!black)
                {
                    foreach (double time in new[] { -0.001, 0, 0.001, -1.0 / 60, 1.0 / 60 })
                    {
                        _idle.Seek(time);
                        await Capture($"seam_{time:F5}");
                    }
                }
                // Capture a complete 60 Hz loop, including the wrap, from the production renderer.
                for (int i = 0; i <= (int)Math.Ceiling(_idle.DurationSeconds * 60) + 2; i++)
                {
                    _idle.Seek(i / 60.0);
                    await Capture($"loop_{i:D4}");
                }
                _idle.Hide();
                var click = new DeniaBubbleBurst { IsBlack = black, Position = new Vector2(296, 300), Scale = Vector2.One * 2 };
                _viewport.AddChild(click);
                click.SetProcess(false);
                for (int i = 0; i < 91; i++)
                {
                    if (i > 0) click._Process(1.0 / 60);
                    if (click.IsQueuedForDeletion()) break;
                    await Capture($"bubble_{i:D3}");
                }
            }
            File.WriteAllText(Path.Combine(_output, "checks.json"),
                $"{{\"checks\":{_checks},\"form\":\"{(black ? "black" : "pink")}\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
            GD.Print($"PASS: {_checks} idle checks, black={black}, render={_render}");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PrintErr(ex); GetTree().Quit(1); }
    }

    private async Task CheckPinkAttack()
    {
        _viewport = new SubViewport { Size = new Vector2I(744, 540), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_viewport);
        var attack = new DeniaPinkAttackVisual();
        attack.Position = attack.DesignOrigin;
        _viewport.AddChild(attack);
        attack.SetProcess(false);
        Check(attack.Initialized, "attack resources initialize");
        var body = attack.GetNode<ColorRect>("FlowBody");
        var material = (ShaderMaterial)body.Material;
        Check(body.Size == new Vector2(744, 540), "larger attack canvas remains unclipped");
        Check(material.GetShaderParameter("frames").As<Texture2D>().GetSize() == new Vector2(5952, 3240), "48 cropped attack tiles");
        Check(material.GetShaderParameter("flow_fields").As<Texture2D>().GetSize() == new Vector2(712, 348), "48 attack flow tiles");
        int completions = 0;
        attack.Finished += () => completions++;
        foreach (int fps in new[] { 30, 60, 144 })
        foreach (int speed in new[] { 1, 3 })
        {
            attack.Play(speed);attack.SetProcess(false);
            for (int f = 0; f < fps; f++) attack._Process(1.0 / fps);
            Check(Math.Abs(attack.Clock - speed) < 1e-8, "attack elapsed time " + fps + " FPS speed " + speed);
            attack._Process(10);
            Check(!attack.Playing && !attack.Visible && attack.Clock == DeniaPinkAttackVisual.Duration, "attack completes once and hides");
            int before = completions;attack._Process(10);
            Check(completions == before, "completion cannot fire twice");
        }
        Check(Math.Abs(DeniaPinkAttackVisual.ImpactTime - 2.083) < 1e-8,
            "damage advances half a second from the original 2.583-second midpoint");
        foreach (int fps in new[] { 30, 60, 144 })
        foreach (int speed in new[] { 1, 3 })
        {
            attack.Play(speed, holdAtImpact: true);attack.SetProcess(false);
            int frames = (int)Math.Ceiling(DeniaPinkAttackVisual.ImpactTime / speed * fps);
            for (int f = 0; f < frames - 1; f++) attack._Process(1.0 / fps);
            Check(!attack.Impact.IsCompleted, "damage waits until impact time at " + fps + " FPS speed " + speed);
            attack._Process(1.0 / fps);
            Check(attack.Impact.IsCompleted && attack.Clock == DeniaPinkAttackVisual.ImpactTime,
                "damage gate lands at exact impact time at " + fps + " FPS speed " + speed);
            attack._Process(1);
            Check(attack.Clock == DeniaPinkAttackVisual.ImpactTime, "impact pose holds until damage callback is ready");
            attack.ReleaseImpact();attack._Process(1.0 / fps);
            Check(attack.Clock > DeniaPinkAttackVisual.ImpactTime, "recovery resumes after damage");
        }
        attack.Play(1, holdAtImpact: true);var canceledImpact = attack.Impact;attack.Stop();
        Check(canceledImpact.IsCompleted, "death or interruption releases pending damage wait");
        attack.Play(1);attack.SetProcess(false);attack._Process(2);attack.Stop();
        int stopped = completions;attack._Process(10);
        Check(completions == stopped && attack.Clock == 2, "interrupted attack stops without completing");
        attack.Play(1);attack.SetProcess(false);
        Check(attack.Clock == 0 && attack.Playing, "next attack restarts from beginning");
        attack.Hide();attack._Process(1);Check(attack.Clock == 0, "hidden form pauses attack");attack.Show();
        foreach (float invalid in new[] { 0f, -1f, float.NaN })
        {
            try { attack.Play(invalid);Check(false, "invalid speed rejected"); }
            catch (ArgumentOutOfRangeException) { Check(true, "invalid speed rejected"); }
        }
        attack.Seek(10);
        Check(attack.Clock == DeniaPinkAttackVisual.Duration && material.GetShaderParameter("frame_index").AsInt32() == 47
            && material.GetShaderParameter("fraction").AsSingle() == 0, "attack final pose clamps without loop");
        var actions = new AnimatedSprite2D { SpriteFrames = new SpriteFrames() };
        actions.SpriteFrames.AddAnimation("idle");actions.SpriteFrames.AddAnimation("attack");
        var rest = GD.Load<Texture2D>("res://images/characters/denia/realtime_pink/action_rest.png");
        actions.SpriteFrames.AddFrame("idle", rest);actions.SpriteFrames.AddFrame("attack", rest);
        var legacyBody = new Node2D();_viewport.AddChild(legacyBody);
        legacyBody.AddChild(actions);actions.Play("idle");
        var idle = new DeniaPinkIdleVisual();legacyBody.AddChild(idle);idle.SetProcess(false);idle.BindActions(actions);
        legacyBody.Hide();actions.Play("attack");actions.Play("idle");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!idle.IsVisibleInTree() && !actions.IsVisibleInTree(), "legacy parent blocks deferred action signals");
        legacyBody.Show();Check(idle.IsVisibleInTree() && !actions.IsVisibleInTree(), "idle restored after attack override");
        legacyBody.Free();
        if (_render)
        {
            for (int i = 0; i <= 310; i++)
            {
                attack.Seek(Math.Min(i / 60.0, DeniaPinkAttackVisual.Duration));
                await Capture($"attack_{i:D4}");
            }
            attack.Seek(2.4);await Capture("attack_right");
            attack.Position = new Vector2(744 - attack.DesignOrigin.X, attack.DesignOrigin.Y);
            attack.SetFacing(true);await Capture("attack_left");
            attack.SetFacing(false);attack.Position = attack.DesignOrigin;
            attack.Modulate = new Color(1, 1, 1, 0.5f);await Capture("attack_half_alpha");attack.Modulate = Colors.White;
            body.Reparent(_viewport);body.Position = Vector2.Zero;attack.Hide();
            foreach (double seconds in new[] { 0, 1.873, 5.165 })
            {
                attack.Seek(seconds);await Capture($"pure_{seconds:F3}");
            }
        }
        if (body.GetParent() != attack) body.Reparent(attack);
        body.Position = -attack.DesignOrigin;attack.Show();
        await CheckAttackController(attack);
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"pink_1hit\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} pink single-hit checks, render={_render}");
    }

    private async Task CheckAttackController(DeniaPinkAttackVisual isolated)
    {
        isolated.Hide();
        var node = ControllerFixture.Create(_viewport);
        var legacy = node.Pink.GetNode<AnimatedSprite2D>("Body/Idle");
        var idle = node.Pink.GetNode<DeniaFlowIdleVisual>("Body/RealtimeIdle");
        foreach (bool fast in new[] { false, true })
        {
            DeniaFormPatch.Fast = fast;
            var current = node.Sync.PlayPinkSingleHit()!;current.SetProcess(false);
            Check(current.Playing && legacy.Animation == "idle" && !legacy.IsPlaying(), "single hit never starts old attack");
            for (int frame = 0; frame <= 310; frame++)
            {
                current.Seek(frame / 60.0);
                // Reproduce late animation signals/direct shows that used to reveal an old body.
                if (frame % 31 == 0) { legacy.Play("attack");legacy.Show();idle.Show();legacy.EmitSignal(AnimatedSprite2D.SignalName.AnimationFinished); }
                node.Sync._Process(1.0 / 60);
                Check(!legacy.IsVisibleInTree() && !idle.IsVisibleInTree() && current.IsVisibleInTree(),
                    "single hit owns body through attack and recovery " + fast + ":" + frame);
            }
            legacy.Play("attack");
            current.Seek(0);current.ReleaseImpact();current._Process(10);
            Check(!current.Playing && !current.IsVisibleInTree() && idle.IsVisibleInTree() && !legacy.IsVisibleInTree()
                && legacy.Animation == "idle" && !legacy.IsPlaying(),
                "controller restores only realtime idle after recovery");
            current = node.Sync.PlayPinkSingleHit()!;current.SetProcess(false);var pending = current.Impact;
            node.Sync.PlayDodge(false);
            Check(!current.Playing && pending.IsCompleted && legacy.IsVisibleInTree() && legacy.Animation == "dodge",
                "dodge interrupts single hit and releases damage wait");
            node.Sync.PlayPinkSingleHit()!.SetProcess(false);node.Entity.Black = true;node.Sync.SetForm(true);
            Check(!current.Playing && !node.Pink.IsVisibleInTree() && node.Black.IsVisibleInTree(), "form switch keeps black body exclusive");
            node.Entity.Black = false;node.Sync.SetForm(false);
        }
        DeniaFormPatch.Fast = false;
        if (_render)
        {
            foreach (double seconds in new[] { 0, 1.873, 2.4 })
            {
                node.Hide();isolated.Show();isolated.Seek(seconds);await Capture($"isolated_{seconds:F3}");isolated.Hide();node.Show();
                var current = node.Sync.PlayPinkSingleHit()!;current.SetProcess(false);current.Seek(seconds);
                legacy.Show();idle.Show();node.Sync._Process(0);
                await Capture($"controller_{seconds:F3}");
            }
        }
        node.Sync.PlayPinkSingleHit()!.SetProcess(false);node.Sync.PlayDeath();
        Check(!node.Pink.GetNode<DeniaPinkAttackVisual>(DeniaPinkAttackVisual.NodeName).Playing
            && legacy.Animation == "death" && legacy.IsVisibleInTree(), "death replaces single hit without overlap");
        node.Free();
    }

    private async Task CheckRise()
    {
        _viewport = new SubViewport { Size = new Vector2I(592, 600), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_viewport);
        var rise = new DeniaBlackRiseVisual();
        rise.Position = rise.DesignOrigin;
        _viewport.AddChild(rise);
        rise.SetProcess(false);
        Check(rise.Initialized, "rise resources initialize");
        var material = (ShaderMaterial)rise.GetNode<ColorRect>("FlowBody").Material;
        Check(material.GetShaderParameter("frames").As<Texture2D>().GetSize() == new Vector2(4736, 3600), "48 rise tiles");
        Check(material.GetShaderParameter("flow_fields").As<Texture2D>().GetSize() == new Vector2(488, 408), "rise flow tiles");
        rise.Seek(-1);
        Check(rise.Clock == 0, "single play clamps negative seek");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            rise.Seek(0);
            for (int f = 0; f < fps * 2; f++) rise._Process(1.0 / fps);
            Check(Math.Abs(rise.Clock - 2) < 1e-8, "rise real time at " + fps);
        }
        rise.Seek(25.5 / 47 * 3);
        Check(material.GetShaderParameter("uBub0").AsVector4().Z == 0
            && material.GetShaderParameter("uBub1").AsVector4().Z == 62, "clean bubble spawn transition");
        rise.Seek(26.5 / 47 * 3);
        Check(material.GetShaderParameter("uBub0").AsVector4() == new Vector4(356, 360, 62, 84)
            && material.GetShaderParameter("uBub1").AsVector4() == new Vector4(358, 361, 56, 108), "liquid ellipse tracking");
        rise.Seek(10);
        Check(rise.Clock == 3 && material.GetShaderParameter("frame_index").AsInt32() == 47
            && material.GetShaderParameter("uFrac").AsSingle() == 0, "last pose clamps without first frame wrap");
        rise.Seek(0);rise.Hide();rise._Process(1);
        Check(rise.Clock == 0, "hidden preview pauses");rise.Show();
        try { rise.Seek(double.NaN);Check(false, "reject invalid time"); }
        catch (ArgumentOutOfRangeException) { Check(true, "reject invalid time"); }
        var stopped = new DeniaBlackRiseVisual();_viewport.AddChild(stopped);
        var completion = stopped.Completion;stopped.Free();
        Check(completion.IsCompleted, "scene exit releases playback waiter");
        if (_render)
        {
            for (int i = 0; i <= 180; i++)
            {
                rise.Seek(i / 60.0);
                await Capture($"rise_{i:D4}");
            }
            rise.Seek(1.7);await Capture("facing_right");
            // Translate back to the canonical canvas so the mirror can be compared pixel-for-pixel.
            rise.Position = new Vector2(592 - rise.DesignOrigin.X, rise.DesignOrigin.Y);
            rise.SetFacing(true);await Capture("facing_left");
        }
        rise.Seek(0);rise._Process(3.1);
        Check(rise.Completion.IsCompleted && rise.Clock == 3, "single play completes at three seconds");
        rise._Process(1);
        Check(rise.Clock == 3, "completed preview stays on final pose");
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"black_rise\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} black rise checks, render={_render}");
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = _viewport.GetTexture().GetImage();
        Check(image.SavePng(Path.Combine(_output, name + ".png")) == Error.Ok, "GPU capture " + name);
        Check(image.GetPixel(0, 0).A < 0.01f && image.GetPixel(image.GetWidth() - 1, image.GetHeight() - 1).A < 0.01f,
            "transparent corners " + name);
    }

    private void CheckBlankUi()
    {
        Check(DeniaBlankClick.IsBlank(null), "empty viewport is blank");
        var layout = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        var label = new Label();
        layout.AddChild(label);
        Check(DeniaBlankClick.IsBlank(layout) && DeniaBlankClick.IsBlank(label), "layout swallowing mouse and plain label stay blank");
        var button = new Button();
        layout.AddChild(button);
        Check(!DeniaBlankClick.IsBlank(button), "native button blocks bubbles");
        var text = new Label { MouseFilter = Control.MouseFilterEnum.Pass };
        button.AddChild(text);
        Check(!DeniaBlankClick.IsBlank(text), "button label resolves clickable ancestor");
        var spacer = new Node2D();
        button.AddChild(spacer);
        var nestedText = new Label { MouseFilter = Control.MouseFilterEnum.Pass };
        spacer.AddChild(nestedText);
        Check(!DeniaBlankClick.IsBlank(nestedText), "Node2D wrapper does not hide a clickable ancestor");
        button.Disabled = true;
        Check(DeniaBlankClick.IsBlank(text), "disabled native button has no click response");
        var callback = new TextureRect();
        layout.AddChild(callback);
        callback.GuiInput += _ => { };
        Check(!DeniaBlankClick.IsBlank(callback), "custom GUI input signal blocks bubbles");
        callback.MouseFilter = Control.MouseFilterEnum.Ignore;
        Check(DeniaBlankClick.IsBlank(callback), "ignored GUI passes through");
        var scripted = new ClickProbe();
        layout.AddChild(scripted);
        Check(!DeniaBlankClick.IsBlank(scripted), "C# GUI input override blocks bubbles");
        layout.Free();
    }

    private void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        _checks++;
    }
}

public partial class ClickProbe : Control
{
    public override void _GuiInput(InputEvent input) { }
}
