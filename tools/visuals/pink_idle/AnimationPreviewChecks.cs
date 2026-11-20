using System;
using System.IO;
using System.Threading.Tasks;
using Denia;
using Godot;

public partial class Preview
{
    private async Task CheckAnimationCatalog()
    {
        _viewport = new SubViewport { Size = new Vector2I(960, 720), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_viewport);
        bool video = Array.IndexOf(OS.GetCmdlineUserArgs(), "video") >= 0;
        DeniaModConfig.UseCodeAnimation = !video;
        CheckCombatAnimationPolicy();
        if (video) CheckVideoCuts();
        foreach (bool black in new[] { false, true })
        foreach (bool fast in new[] { false, true })
        foreach (var kind in Enum.GetValues<DeniaAnimationPreviewKind>())
        {
            if (kind == DeniaAnimationPreviewKind.Execution) continue;
            bool unavailable = video
                ? kind is DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown or DeniaAnimationPreviewKind.BlackRise
                : kind is DeniaAnimationPreviewKind.BlackTriple or DeniaAnimationPreviewKind.PinkCollapse;
            DeniaFormPatch.Fast = fast;
            var node = ControllerFixture.Create(_viewport);
            node.Entity.Black = black; node.Sync.SetForm(black);
            var playing = node.Sync.PlayAnimationPreview(kind);
            if (unavailable)
            {
                Check(playing.IsCompletedSuccessfully && !node.Visuals.HasNode("AnimationPreview"),
                    $"{kind}: no preview from the other renderer");
                node.Free();
                continue;
            }
            var preview = node.Visuals.GetNode<DeniaAnimationPreview>("AnimationPreview");
            preview.SetProcess(false);
            Check(!playing.IsCompleted, $"{kind}: preview awaits playback");
            Check(node.Entity.Black == black, $"{kind}: preview preserves form");
            Check(preview.ReplacesBody ? !node.Pink.Visible && !node.Black.Visible
                : node.Pink.Visible == !black && node.Black.Visible == black, $"{kind}: exclusive body or overlay particles");
            double speed = fast ? 3 : 1;
            double expected = kind switch {
                DeniaAnimationPreviewKind.PinkIdle => 10.632,
                DeniaAnimationPreviewKind.BlackIdle => 5.316,
                DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown => 3.083 / speed,
                DeniaAnimationPreviewKind.BlackRise => 3,
                DeniaAnimationPreviewKind.PinkSingle => 11.332 / speed,
                DeniaAnimationPreviewKind.PinkMulti => 20.829333 / speed,
                DeniaAnimationPreviewKind.BlackSingle => 7.933 / speed,
                DeniaAnimationPreviewKind.BlackMulti or DeniaAnimationPreviewKind.BlackTriple => 8.783 / speed,
                DeniaAnimationPreviewKind.PinkCollapse => 7.189,
                DeniaAnimationPreviewKind.BlackFour => 11.466 / speed,
                DeniaAnimationPreviewKind.MapBubble => 1.5 / 0.55,
                _ => 1.5 };
            if (video)
            {
                expected = kind switch {
                    DeniaAnimationPreviewKind.PinkIdle => 14.5,
                    DeniaAnimationPreviewKind.BlackIdle => 2.983333,
                    DeniaAnimationPreviewKind.PinkSingle => 4.566667 / speed,
                    DeniaAnimationPreviewKind.PinkMulti => 7.016667 / speed,
                    DeniaAnimationPreviewKind.PinkCollapse => 7.183333,
                    DeniaAnimationPreviewKind.BlackSingle => 9.783333 / speed,
                    DeniaAnimationPreviewKind.BlackMulti => 8.166667 / speed,
                    DeniaAnimationPreviewKind.BlackTriple => 8.433333 / speed,
                    DeniaAnimationPreviewKind.BlackFour => 11.916667 / speed,
                    _ => expected };
            }
            Check(Math.Abs(preview.Duration - expected) < 1e-6, $"{kind}: normal/Fast duration");
            var flow = preview.GetChildCount() > 0 ? preview.GetChild(0) as DeniaFlowIdleVisual : null;
            if (flow != null)
                Check(flow.UsingVideo == (video && kind != DeniaAnimationPreviewKind.BlackRise), $"{kind}: selected renderer");
            if (kind == DeniaAnimationPreviewKind.PinkSitdown)
                Check(flow!.Clock == flow.PlaybackDuration, "reverse starts at standing endpoint");
            int steps = (int)Math.Ceiling(expected * 60);
            for (int i = 0; i < steps; i++)
            {
                preview._Process(1.0 / 60);
                if (i + 1 < steps) Check(!playing.IsCompleted, $"{kind}: cannot finish early");
                if (_render && !black && !fast && i == steps / 2)
                    await Capture($"catalog_{kind}");
            }
            if (!playing.IsCompleted) preview._Process(1e-6); // floating-point endpoint tolerance
            await playing;
            Check(node.Entity.Black == black && node.Pink.Visible == !black && node.Black.Visible == black,
                $"{kind}: restores current idle after playback");
            if (kind == DeniaAnimationPreviewKind.PinkSitdown)
                Check(flow!.Clock == 0, "reverse finishes at seated endpoint");
            node.Free();
        }
        // Scene unload must release the awaiting test card even halfway through an attack.
        var cancelled = ControllerFixture.Create(_viewport);
        var pending = cancelled.Sync.PlayAnimationPreview(DeniaAnimationPreviewKind.PinkMulti);
        cancelled.Free(); await pending;
        Check(pending.IsCompletedSuccessfully, "scene unload releases preview waiter");
        _viewport.Free(); _viewport = null!;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect(); GC.WaitForPendingFinalizers();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"catalog\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} animation catalog checks, render={_render}");
    }

    private void CheckVideoCuts()
    {
        foreach (bool fast in new[] { false, true })
        foreach (bool multi in new[] { false, true })
        {
            DeniaFormPatch.Fast = fast;
            var node = ControllerFixture.Create(_viewport);
            var idle = node.Pink.GetNode<DeniaFlowIdleVisual>("Body/RealtimeIdle");
            idle.Seek(0.75);
            double idleClock = idle.Clock;
            var attack = node.Sync.PlaySingleHit(false, multi)!;
            attack.SetProcess(false);
            Check(attack.Clock == 0 && attack.LeadInDuration == 0 && attack.ReturnDuration == 0,
                "pink video has only the attack timeline");
            Check(!idle.IsVisibleInTree() && attack.IsVisibleInTree() && !attack.HasNode("PinkHandoff"),
                "first attack frame directly replaces idle without a handoff viewport");
            double speed = fast ? 3 : 1;
            attack._Process(0.01 / speed);
            Check(Math.Abs(attack.Clock - 0.01) < 1e-6, "the first tick advances the attack itself");
            attack.ReleaseRemainingImpacts(attack.Impact);
            attack._Process((attack.PlaybackDuration - attack.Clock - 0.001) / speed);
            Check(attack.IsVisibleInTree() && !idle.IsVisibleInTree(), "last attack frame still owns the body");
            attack._Process(0.002 / speed);
            Check(!attack.IsVisibleInTree() && idle.IsVisibleInTree() && idle.Clock == idleClock,
                "end of video restores the same idle clock with a hard cut");
            node.Free();
        }
        DeniaFormPatch.Fast = false;

        var missing = new DeniaPinkIdleVisual { VideoClip = "missing_fixture_clip" };
        _viewport.AddChild(missing);
        missing.Seek(0.5);
        Check(missing.VideoPlaybackRequested && !missing.UsingVideo && !missing.HasBody && !missing.HasNode("FlowBody"),
            "missing video does not initialize a code animation");
        missing.SetVideoPlayback(false);
        Check(missing.HasBody && missing.GetNode<ColorRect>("FlowBody").Visible, "code body initializes when explicitly selected");
        missing.SetVideoPlayback(true);
        Check(!missing.GetNode<ColorRect>("FlowBody").Visible, "returning to video keeps the code body hidden on load failure");
        missing.Free();
    }

    private void CheckCombatAnimationPolicy()
    {
        foreach (bool black in new[] { false, true })
        foreach (bool fast in new[] { false, true })
        foreach (int hits in new[] { 1, 2, 3, 4 })
        {
            DeniaFormPatch.Fast = fast;
            var node = ControllerFixture.Create(_viewport);
            node.Entity.Black = black; node.Sync.SetForm(black);
            var idle = (black ? node.Black : node.Pink).GetNode<DeniaFlowIdleVisual>("Body/RealtimeIdle");
            Check(idle.UsingVideo != DeniaModConfig.UseCodeAnimation, "idle uses selected renderer");
            var attack = node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3)!;
            Check(attack.UsingVideo != DeniaModConfig.UseCodeAnimation, "attack uses selected renderer");
            if (attack.UsingVideo)
            {
                double[] expected = (black, hits) switch {
                    (false, 1) => [50 / 60.0],
                    (false, _) => [55 / 60.0, 102 / 60.0],
                    (true, 1) => [26 / 60.0],
                    (true, 2) => [55 / 60.0, 81 / 60.0],
                    (true, 3) => [77 / 60.0, 102 / 60.0, 122 / 60.0],
                    _ => [41 / 60.0, 68 / 60.0, 88 / 60.0, 117 / 60.0] };
                Check(attack.PlaybackImpactCount == expected.Length, "video retains every source impact");
                for (int i = 0; i < expected.Length; i++)
                    Check(Math.Abs(expected[i] - attack.GetPlaybackImpactTime(i)) < 1e-6,
                        "impact table follows source frame");
            }
            int expectedDamageImpacts = hits == 1 ? 1 : black && hits == 4 ? 4 : 2;
            double speed = fast ? 3 : 1;
            attack.SetProcess(false);
            var first = attack.Impact;
            Check(attack.WaitForDamage == (black || !fast), "Fast pink skips damage waits in both renderers");
            Check(attack.DamageImpactCount == expectedDamageImpacts, "damage gates follow the code attack hit count");
            Check(attack.CanBeInterrupted == fast, "only Fast permits interruption before damage");
            if (fast)
            {
                // 模拟下个动作在起身／首挥击之前到来；旧门控必须立即释放，不影响新实例门控。
                var last = attack.ImpactAt(expectedDamageImpacts - 1);
                var next = node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3);
                Check(next != null && first.IsCompleted && last.IsCompleted && next.Impact != first,
                    "Fast replaces pre-hit animation and releases old waits");
                attack = next!;
                attack.SetProcess(false);
                attack.ReleaseRemainingImpacts(first);
                first = attack.Impact;
            }
            else
            {
                Check(node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3) == null && !first.IsCompleted,
                    "Normal cannot replace protected intro or swing");
            }

            if (!attack.WaitForDamage)
            {
                double lastImpact = attack.GetPlaybackImpactTime(attack.PlaybackImpactCount - 1);
                attack._Process((attack.LeadInDuration + lastImpact + 0.3) / speed);
                Check(attack.Clock > lastImpact, "Fast pink advances through every swing without a damage callback");
            }
            else
            {
                for (int i = 0; i < expectedDamageImpacts; i++)
                {
                    double impactTime = attack.GetPlaybackImpactTime(i);
                    attack.SeekTimeline(attack.LeadInDuration + impactTime - 0.001);
                    attack._Process(0.0005 / speed);
                    Check(!attack.ImpactAt(i).IsCompleted, "damage cannot precede the selected frame");
                    attack._Process(0.002 / speed);
                    Check(attack.ImpactAt(i).IsCompleted && Math.Abs(attack.Clock - impactTime) < 1e-6,
                        "damage gate holds picture at the selected impact");
                    attack.ReleaseImpact(attack.ImpactAt(i));
                    Check(attack.CanBeInterrupted == fast, "only Fast can replace before damage is confirmed");
                    attack.ConfirmDamage(first);
                    Check(attack.CanBeInterrupted == (fast || i + 1 == expectedDamageImpacts),
                        "Normal unlocks only after the required actual damage rounds");
                }
                Check(attack.ImpactAt(expectedDamageImpacts).IsCompleted, "later damage adds no animation wait");
                if (attack.PlaybackImpactCount > expectedDamageImpacts)
                {
                    double thirdSwing = attack.GetPlaybackImpactTime(expectedDamageImpacts);
                    attack._Process((thirdSwing - attack.Clock + 0.1) / speed);
                    Check(attack.Clock > thirdSwing, "video-only third swing has no damage hold");
                }
                var next = node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3);
                Check(next != null && first.IsCompleted, "recovery can be replaced after confirmed numbers");
                next!.SetProcess(false);
            }
            node.Free();
        }
        DeniaFormPatch.Fast = false;
    }
}
