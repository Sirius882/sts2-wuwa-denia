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
        foreach (bool black in new[] { false, true })
        foreach (bool fast in new[] { false, true })
        foreach (var kind in Enum.GetValues<DeniaAnimationPreviewKind>())
        {
            if (kind == DeniaAnimationPreviewKind.Execution) continue;
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
                DeniaAnimationPreviewKind.BlackMulti => 8.783 / speed,
                DeniaAnimationPreviewKind.BlackFour => 11.466 / speed,
                DeniaAnimationPreviewKind.MapBubble => 1.5 / 0.55,
                _ => 1.5 };
            Check(Math.Abs(preview.Duration - expected) < 1e-6, $"{kind}: normal/Fast duration");
            var flow = preview.GetChildCount() > 0 ? preview.GetChild(0) as DeniaFlowIdleVisual : null;
            if (kind == DeniaAnimationPreviewKind.PinkSitdown)
                Check(flow!.Clock == DeniaPinkStandupVisual.Duration, "reverse starts at standing endpoint");
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
}
