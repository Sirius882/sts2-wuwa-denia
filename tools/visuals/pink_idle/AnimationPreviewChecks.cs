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
        foreach (bool black in new[] { false, true })
        foreach (bool fast in new[] { false, true })
        foreach (var kind in Enum.GetValues<DeniaAnimationPreviewKind>())
        {
            if (kind == DeniaAnimationPreviewKind.Execution || (!video
                && kind is DeniaAnimationPreviewKind.BlackTriple or DeniaAnimationPreviewKind.PinkCollapse)) continue;
            DeniaFormPatch.Fast = fast;
            var node = ControllerFixture.Create(_viewport);
            node.Entity.Black = black; node.Sync.SetForm(black);
            var playing = node.Sync.PlayAnimationPreview(kind);
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
                const double standup = 3.083333;
                expected = kind switch {
                    DeniaAnimationPreviewKind.PinkIdle => 14.5,
                    DeniaAnimationPreviewKind.BlackIdle => 2.983333,
                    DeniaAnimationPreviewKind.PinkStandup or DeniaAnimationPreviewKind.PinkSitdown => standup / speed,
                    DeniaAnimationPreviewKind.PinkSingle => (2 * standup + 4.566667) / speed,
                    DeniaAnimationPreviewKind.PinkMulti => (2 * standup + 7.016667) / speed,
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
            if (black && hits == 3)
                Check(attack.PlaybackImpactCount == (attack.UsingVideo ? 3 : 2), "only video uses third impact");
            attack.SetProcess(false);
            var first = attack.Impact;
            Check(attack.WaitForDamage == (attack.UsingVideo || black || !fast), "only code Fast pink skips damage waits");
            Check(attack.CanBeInterrupted == fast, "table: only Fast permits interruption before damage");
            if (fast)
            {
                // 模拟下个动作在起身／首挥击之前到来；旧门控必须立即释放，不影响新实例门控。
                var next = node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3);
                Check(next != null && first.IsCompleted && next.Impact != first,
                    "Fast replaces pre-hit animation and releases old waits");
                next!.SetProcess(false);
                if (!black && !next.UsingVideo)
                {
                    next._Process((next.LeadInDuration + next.DamageTime + 0.3) / 3);
                    Check(next.Clock > next.DamageTime, "Fast pink advances past impact without a damage callback");
                }
            }
            else
            {
                Check(node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3) == null && !first.IsCompleted,
                    "Normal cannot replace protected intro or swing");
                for (int i = 0; i < attack.PlaybackImpactCount; i++)
                {
                    double impactTime = attack.GetPlaybackImpactTime(i);
                    if (attack.UsingVideo)
                    {
                        double[] expected = (black, hits) switch {
                            (false, 1) => [50 / 60.0],
                            (false, _) => [55 / 60.0, 102 / 60.0],
                            (true, 1) => [26 / 60.0],
                            (true, 2) => [55 / 60.0, 81 / 60.0],
                            (true, 3) => [77 / 60.0, 102 / 60.0, 122 / 60.0],
                            _ => [41 / 60.0, 68 / 60.0, 88 / 60.0, 117 / 60.0] };
                        Check(Math.Abs(expected[i] - impactTime) < 1e-6, "impact table follows source frame");
                    }
                    attack.SeekTimeline(attack.LeadInDuration + impactTime - 0.001);
                    attack._Process(0.0005);
                    Check(!attack.ImpactAt(i).IsCompleted, "damage cannot precede the selected frame");
                    attack._Process(0.002);
                    Check(attack.ImpactAt(i).IsCompleted && Math.Abs(attack.Clock - impactTime) < 1e-6,
                        "damage gate holds picture at the selected impact");
                    attack.ReleaseImpact(attack.ImpactAt(i));
                    Check(!attack.CanBeInterrupted, "Normal releasing a pre-damage gate alone cannot unlock replacement");
                    attack.ConfirmDamage(first);
                    Check(attack.CanBeInterrupted == (i + 1 == attack.PlaybackImpactCount),
                        "Normal unlocks only after the required actual damage rounds");
                }
                var next = node.Sync.PlaySingleHit(black, hits >= 2, hits >= 4, hits == 3);
                Check(next != null && first.IsCompleted, "Normal can replace recovery after confirmed numbers");
                next!.SetProcess(false);
            }
            node.Free();
        }
        DeniaFormPatch.Fast = false;
    }
}
