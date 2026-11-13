using System;
using System.Threading.Tasks;
using Godot;

namespace Denia;

/// <summary>备用黑态起身：原始48幅素材的实时光流、液态凝泡和代码星芒；单次3秒。</summary>
public partial class DeniaBlackRiseVisual : DeniaFlowIdleVisual
{
    public const double Duration = 3;
    protected override string Assets => "res://images/characters/denia/realtime_black_rise/";
    protected override string ShaderPath => "res://shaders/denia_black_rise.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 48;
    public override float LayoutHeight => 544;
    public override Vector2 DesignOrigin => new(338, 568);
    protected override bool Loop => false;
    private float[] _cx = null!, _rx = null!, _ry = null!;
    private readonly TaskCompletionSource _completion = new();
    public Task Completion => _completion.Task;

    private static readonly Vector2[] BubblePositions = [new(256, 455), new(332, 478), new(438, 410), new(465, 365), new(125, 345)];
    private static readonly Vector2[] BubbleDrifts = [new(12, -95), new(-8, -110), new(10, -80), new(6, -70), new(-10, -65)];
    private static readonly float[] BubbleRadii = [7, 8.2f, 6.2f, 7.5f, 6.4f];
    private static readonly float[] BubbleSpeeds = [0.65f, 0.58f, 0.72f, 0.62f, 0.54f];
    private static readonly float[] BubblePhases = [0.1f, 0.7f, 1.4f, 2.1f, 2.8f];
    private static readonly Vector2[] StarPositions = [new(-14, 12), new(-24, 9), new(-25, -15), new(-6, -10)];
    private static readonly float[] StarSizes = [7.8f, 5.8f, 4.6f, 5];
    private static readonly float[] StarSpeeds = [3, 3.6f, 2.7f, 3.3f];
    private static readonly float[] StarPhases = [0.2f, 1.5f, 2.7f, 4.1f];
    private static readonly Color[] StarColors = [new("d8fbff"), new("9ee8ff"), new("ffd6ec"), new("ffcae8")];

    public override void _Ready()
    {
        var data = GD.Load<Resource>(Assets + "tracking.tres");
        _cx = data.GetMeta("bubble_cx").AsFloat32Array();
        _rx = data.GetMeta("bubble_rx").AsFloat32Array();
        _ry = data.GetMeta("bubble_ry").AsFloat32Array();
        if (_cx.Length != FrameCount || _rx.Length != FrameCount || _ry.Length != FrameCount)
            throw new InvalidOperationException("Black rise requires 48 liquid bubble samples.");
        base._Ready();
    }

    private Vector4 Bubble(int frame) => new(_cx[frame], BubbleCy[frame], _rx[frame], _ry[frame]);

    private Vector4 CurrentBubble()
    {
        double f = Clock / Duration * 47;
        int i = Math.Min((int)f, 47);
        return Bubble(i).Lerp(Bubble(Math.Min(i + 1, 47)), (float)(f - i));
    }

    protected override void UpdateFlowParameters()
    {
        double f = Clock / Duration * 47;
        int i = Math.Min((int)f, 47);
        FlowMaterial.SetShaderParameter("uFrac", (float)(f - i));
        FlowMaterial.SetShaderParameter("uTime", (float)Clock);
        FlowMaterial.SetShaderParameter("uBub0", Bubble(i));
        FlowMaterial.SetShaderParameter("uBub1", Bubble(Math.Min(i + 1, 47)));
        FlowMaterial.SetShaderParameter("uHalo", new Vector2(Track(OrbX), Track(OrbY)));
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Clock >= Duration) _completion.TrySetResult();
    }

    public override void _ExitTree() => _completion.TrySetResult();

    public override void _Draw()
    {
        if (!Initialized) return;
        float t = (float)Clock;
        DrawSetTransform(new Vector2(Flip ? DesignOrigin.X : -DesignOrigin.X, -DesignOrigin.Y), 0,
            new Vector2(Flip ? -1 : 1, 1));
        var b = CurrentBubble();
        if (b.Z > 20 && b.W > 20)
        {
            float alpha = Mathf.Clamp((b.Z - 20) / 25, 0, 1);
            for (int i = 0; i < 4; i++)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * StarSpeeds[i] + StarPhases[i]);
                Vector2 pos = new Vector2(b.X, b.Y) + StarPositions[i] * new Vector2(b.Z / 74, b.W / 74)
                    + new Vector2(Mathf.Sin(t * 1.1f + StarPhases[i]), Mathf.Cos(t * 0.9f + StarPhases[i])) * 1.2f;
                float size = StarSizes[i] * (0.75f + 0.35f * pulse) * Mathf.Clamp(b.Z / 74, 0.5f, 1);
                // The shared star is normalized to 1.35 rather than the source's unit radius.
                DeniaBlackIdleVisual.DrawFourPointStar(this, pos, size / 1.35f, StarColors[i],
                    StarColors[i], alpha * (0.55f + 0.35f * pulse));
            }
        }
        for (int i = 0; i < 5; i++)
        {
            float cycle = ((t * BubbleSpeeds[i] + BubblePhases[i]) % 2.4f) / 2.4f;
            float alpha = Mathf.Sin(cycle * Mathf.Pi);
            if (alpha <= 0.06f) continue;
            Vector2 pos = BubblePositions[i] + BubbleDrifts[i] * cycle + new Vector2(Mathf.Sin(t * 2.1f + i) * 3.5f, 0);
            float radius = BubbleRadii[i] * (0.82f + 0.22f * alpha);
            DrawCircle(pos, radius, new Color(95 / 255f, 205 / 255f, 1, alpha * 0.92f), true, -1, true);
            DrawCircle(pos, radius * 0.9f, new Color(18 / 255f, 24 / 255f, 56 / 255f, alpha * 0.9f), true, -1, true);
            DrawCircle(pos - Vector2.One * radius * 0.28f, radius * 0.55f,
                new Color(48 / 255f, 68 / 255f, 128 / 255f, alpha * 0.82f), true, -1, true);
            DrawArc(pos, radius, 0, Mathf.Tau, 40, new Color(140 / 255f, 225 / 255f, 1, alpha * 0.65f), 0.85f, true);
            DrawCircle(pos - Vector2.One * radius * 0.34f, Mathf.Max(0.9f, radius * 0.18f),
                new Color(225 / 255f, 248 / 255f, 1, alpha * 0.72f), true, -1, true);
        }
        Vector2 halo = new(Track(OrbX), Track(OrbY));
        float hp = 0.75f + 0.25f * Mathf.Sin(t * 3.8f);
        for (int i = 20; i > 0; i--)
            DrawCircle(halo, i * hp, new Color(70 / 255f, 185 / 255f, 1, (1 - i / 21f) * 0.018f), true, -1, true);
    }
}
