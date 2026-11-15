using System;
using System.IO;
using System.Threading.Tasks;
using Denia;
using Godot;

public partial class Preview
{
    private async Task CheckPinkStandup()
    {
        double lead = DeniaPinkStandupVisual.Duration;
        _viewport = new SubViewport { Size = new Vector2I(1280, 960), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_viewport);
        var node = ControllerFixture.Create(_viewport);
        node.LayoutPosition = new Vector2(527, 400);
        DeniaFormPatch.SyncOverlayLayout(node);
        var idle = node.Pink.GetNode<DeniaFlowIdleVisual>("Body/RealtimeIdle");
        idle.SetProcess(false);
        var legacy = node.Pink.GetNode<AnimatedSprite2D>("Body/Idle");
        foreach (bool multi in new[] { false, true })
        {
            string tag = multi ? "combo" : "single";
            foreach (int fps in new[] { 30, 60, 144 })
            foreach (int speed in new[] { 1, 3 })
            {
                DeniaFormPatch.Fast = speed == 3;
                var attack = node.Sync.PlaySingleHit(false, multi)!;attack.SetProcess(false);
                Check(attack.LeadInDuration == lead && attack.IsPreparing && attack.Clock == 0,
                    tag + " begins with full native standup");
                Check(!legacy.IsVisibleInTree() && !idle.IsVisibleInTree(), "legacy and idle body stay exclusive");
                for (int frame = 1; frame <= (int)Math.Floor(lead * fps / speed); frame++)
                {
                    attack._Process(1.0 / fps);
                    Check(attack.Clock == 0 && !attack.Impact.IsCompleted, "standup cannot advance attack or damage");
                }
                for (int hit = 0; hit < attack.ImpactCount; hit++)
                {
                    double due = lead + attack.GetImpactTime(hit);
                    while (attack.TimelineClock + speed / (double)fps < due - 1e-10) attack._Process(1.0 / fps);
                    Check(!attack.ImpactAt(hit).IsCompleted, tag + " damage waits through intro and swing");
                    attack._Process(1.0 / fps + 1e-9);
                    Check(attack.ImpactAt(hit).IsCompleted && attack.Clock == attack.GetImpactTime(hit),
                        tag + " exact shifted damage pose " + fps + "/" + speed + "/" + hit);
                    attack._Process(0.5);
                    Check(attack.Clock == attack.GetImpactTime(hit), "damage callback retains hold at exact pose");
                    attack.ReleaseImpact(attack.ImpactAt(hit));
                }
                Check(attack.ImpactAt(attack.ImpactCount).IsCompleted, "later damage adds no animation wait");
                attack.SeekTimeline(lead + attack.DurationSeconds);
                Check(attack.IsReturning && attack.Playing, "attack end begins reverse standup before restoring idle");
                for (int frame = 1; frame < (int)Math.Floor(lead * fps / speed); frame++)
                {
                    attack._Process(1.0 / fps);
                    Check(attack.Playing && attack.Clock == attack.DurationSeconds && !idle.IsVisibleInTree(),
                        "reverse standup owns body and never restarts damage timeline");
                }
                attack._Process(30);
                Check(!attack.Playing && idle.IsVisibleInTree(), "one sequence finishes and restores idle");
                double expectedIdle = ((attack.IdleHandoffClock % idle.DurationSeconds) + idle.DurationSeconds) % idle.DurationSeconds;
                Check(Math.Abs(idle.Clock - expectedIdle) < 1e-8, "return hands exact final seated phase to live idle");
            }
            DeniaFormPatch.Fast = false;
            var current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);
            current._Process(lead + 0.125);
            Check(Math.Abs(current.Clock - 0.125) < 1e-9 && !current.IsPreparing,
                "large delta carries only time remaining after intro");
            current.Stop();
            foreach (double t in new[] { 0.0, 0.07, lead - 0.08, lead + 0.2 })
            {
                current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);current.SeekTimeline(t);
                var pending = current.Impact;var second = current.ImpactAt(1);
                node.Sync.SetForm(false);
                Check(!current.Playing && pending.IsCompleted && second.IsCompleted && legacy.Animation == "idle" && !legacy.IsVisibleInTree(),
                    "form refresh cancels intro/attack and every pending damage wait");
            }
            current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);current.SeekTimeline(0.8);
            var old = current.Impact;
            current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);
            Check(old.IsCompleted && current.TimelineClock == 0 && !current.Impact.IsCompleted,
                "replay restarts intro and releases only old damage gates");
            node.Entity.Black = true;node.Sync.SetForm(true);
            Check(!current.Playing && current.Impact.IsCompleted && !node.Pink.IsVisibleInTree(), "form switch cancels intro");
            var black = node.Sync.PlaySingleHit(true, multi)!;black.SetProcess(false);
            Check(black.LeadInDuration == 0 && !black.IsPreparing, "black timing remains unchanged");
            node.Entity.Black = false;node.Sync.SetForm(false);

            if (_render)
            {
                idle.Seek(1.23);await Capture(tag + "_idle_before");
                current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);current.SeekTimeline(0);
                await Capture(tag + "_entry_zero");
                foreach (double t in new[] { 0.0, 0.07, 0.14, 0.7, 1.6, 2.7, lead - 0.16, lead - 0.08, lead, lead + 0.1 })
                {
                    current.SeekTimeline(t);await Capture($"{tag}_join_{t:F3}");
                    current.SetFacing(true);await Capture($"{tag}_mirror_{t:F3}");current.SetFacing(false);
                }
                current.SeekTimeline(lead);await Capture(tag + "_attack_start");
                current.Play(1);current.SetProcess(false);await Capture(tag + "_attack_original");
                current = node.Sync.PlaySingleHit(false, multi)!;current.SetProcess(false);
                // 30 Hz full sequence: hold-free seeking preserves every source pose and exact total length.
                for (int frame = 0; frame <= (int)Math.Ceiling(current.TotalDuration * 30); frame++)
                {
                    current.SeekTimeline(frame / 30.0);await Capture($"standup_{tag}_{frame:D4}");
                }
                double returnStart = lead + current.DurationSeconds;
                foreach (double t in new[] { 0.0, 0.08, 0.16, 0.7, 1.7, lead - 0.14, lead - 0.07, lead })
                {
                    current.SeekTimeline(returnStart + t);await Capture($"{tag}_return_{t:F3}");
                }
                current.SeekTimeline(current.TotalDuration);await Capture(tag + "_return_seated");
                current.ReleaseRemainingImpacts(current.Impact);current._Process(30);
                await Capture(tag + "_idle_after");
            }
        }
        var dying = node.Sync.PlaySingleHit(false, true)!;dying.SetProcess(false);dying.SeekTimeline(0.8);
        node.Sync.PlayDeath();
        Check(!dying.Playing && dying.ImpactAt(1).IsCompleted && !node.Pink.IsVisibleInTree() && !node.Black.IsVisibleInTree()
            && node.Corpse.IsVisibleInTree() && node.Sync.DeathDuration == 0, "death cancels intro and shows static energy with no animation wait");
        node.Free();
        var pure = new DeniaPinkStandupVisual();_viewport.AddChild(pure);pure.SetProcess(false);
        Check(pure.Initialized, "74-frame standup resources initialize");
        _viewport.Size = new Vector2I(504, 768);pure.Position = pure.DesignOrigin;
        var pureBody = pure.GetNode<ColorRect>("FlowBody");pureBody.Reparent(_viewport, false);pureBody.Position = Vector2.Zero;
        if (_render)
            foreach (double t in new[] { 0.0, 0.413, 1.7, lead })
            {
                pure.Seek(t);await Capture($"standup_glsl_{t:F3}");
            }
        pure.Free();pureBody.Free();
        File.WriteAllText(Path.Combine(_output, "checks.json"),
            $"{{\"checks\":{_checks},\"form\":\"pink_standup\",\"render\":{_render.ToString().ToLowerInvariant()}}}");
        GD.Print($"PASS: {_checks} standup sequence checks, render={_render}");
    }
}
