using Godot;

namespace Denia;

/// <summary>黑态原 JS：闭环光流、深色泡泡波纹／虹彩、星眸光环和代码十字星。</summary>
public partial class DeniaBlackIdleVisual : DeniaFlowIdleVisual
{
    public const double Duration = 5.316;
    public const float ReferenceHeight = 536f;
    public static readonly Vector2 Origin = new(240, 600);
    protected override string Assets => "res://images/characters/denia/realtime_black/";
    protected override string ShaderPath => "res://shaders/denia_black_idle.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 48;
    public override float LayoutHeight => ReferenceHeight;
    public override Vector2 DesignOrigin => Origin;
    protected override bool WrapFrames => true;

    private static readonly Vector2[] BubblePositions = [new(382, 365), new(364, 380), new(344, 242), new(175, 348), new(356, 432)];
    private static readonly Vector2[] BubbleDrifts = [new(6.5f, -22), new(4.5f, -18), new(3.5f, -15), new(-5.5f, -20), new(5, -16)];
    private static readonly float[] BubbleRadii = [9.8f, 7.4f, 4.2f, 6.2f, 5];
    private static readonly float[] BubbleSpeeds = [0.42f, 0.48f, 0.55f, 0.38f, 0.51f];
    private static readonly float[] BubblePhases = [0, 1.1f, 2.3f, 3.6f, 4.8f];
    private static readonly Vector2[] StarPositions = [new(-17, 14), new(-27, 11), new(-29, -16), new(-8, -11)];
    private static readonly float[] StarSizes = [8.5f, 6.2f, 4.8f, 5.2f];
    private static readonly float[] StarSpeeds = [2.8f, 3.4f, 2.5f, 3.1f];
    private static readonly float[] StarPhases = [0.2f, 1.5f, 2.7f, 4.1f];
    private static readonly Color[] StarColors = [new("d8fbff"), new("9ee8ff"), new("ffd6ec"), new("ffcae8")];
    private static readonly Vector2[] StarShape = MakeStarShape();

    protected override void UpdateFlowParameters()
    {
        FlowMaterial.SetShaderParameter("clock", (float)Clock);
        FlowMaterial.SetShaderParameter("bubble", new Vector3(265, Track(BubbleCy), 76));
        FlowMaterial.SetShaderParameter("halo", new Vector2(Track(OrbX), Track(OrbY)));
    }

    public override void _Draw()
    {
        if (BubbleCy == null) return;
        float t = (float)Clock, cy = Track(BubbleCy);
        DrawSetTransform(new Vector2(Flip ? Origin.X : -Origin.X, -Origin.Y), 0,
            new Vector2(Flip ? -1 : 1, 1));
        Vector2 center = new(265, cy);
        // Original rotating linear gradient on the bottom/left unoccluded bubble arc.
        for (int i = 0; i < 64; i++)
        {
            float angle = Mathf.Lerp(0.32f, Mathf.Pi * 1.38f, (i + 0.5f) / 64);
            float gradient = (1 - Mathf.Cos(angle - t * 0.55f)) * 0.5f;
            Color color = gradient < 0.5f
                ? new Color(130 / 255f, 225 / 255f, 1, 0.82f).Lerp(new Color(55 / 255f, 155 / 255f, 1, 0.38f), gradient * 2)
                : new Color(55 / 255f, 155 / 255f, 1, 0.38f).Lerp(new Color(170 / 255f, 235 / 255f, 1, 0.85f), gradient * 2 - 1);
            DrawLine(center + Vector2.FromAngle(Mathf.Lerp(0.32f, Mathf.Pi * 1.38f, i / 64f)) * 76,
                center + Vector2.FromAngle(Mathf.Lerp(0.32f, Mathf.Pi * 1.38f, (i + 1) / 64f)) * 76, color, 1.65f, true);
        }
        for (int i = 0; i < 4; i++)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(t * StarSpeeds[i] + StarPhases[i]);
            Vector2 pos = center + StarPositions[i] + new Vector2(Mathf.Sin(t * 1.2f + i), Mathf.Cos(t * 1.4f + i)) * 1.2f;
            DrawStar(pos, StarSizes[i] * (0.75f + 0.35f * pulse), StarColors[i],
                i == 2 ? new Color("fff5fa") : Colors.White, pulse * 0.88f);
        }
        float haloPulse = 0.72f + 0.28f * Mathf.Sin(t * 3.4f);
        DrawStar(new Vector2(Track(OrbX), Track(OrbY)), 9.5f * haloPulse,
            new Color(135 / 255f, 225 / 255f, 1, 0.85f), Colors.White, haloPulse * 0.9f);

        for (int i = 0; i < 5; i++)
        {
            float cycle = ((t * BubbleSpeeds[i] + BubblePhases[i]) % 2.8f) / 2.8f;
            float alpha = Mathf.Sin(cycle * Mathf.Pi);
            if (alpha <= 0.05f) continue;
            Vector2 pos = BubblePositions[i] + BubbleDrifts[i] * cycle
                + new Vector2(Mathf.Sin(t * 2.1f + i) * 2.5f, cy - 388);
            float radius = BubbleRadii[i] * (0.75f + 0.3f * alpha);
            // Native concentric fills approximate the source's dark radial gradient.
            DrawCircle(pos, radius, new Color(75 / 255f, 175 / 255f, 1, alpha * 0.95f), true, -1, true);
            DrawCircle(pos, radius * 0.85f, new Color(10 / 255f, 22 / 255f, 68 / 255f, alpha * 0.92f), true, -1, true);
            DrawCircle(pos - Vector2.One * radius * 0.2f, radius * 0.45f,
                new Color(28 / 255f, 62 / 255f, 148 / 255f, alpha * 0.85f), true, -1, true);
            DrawArc(pos, radius, 0, Mathf.Tau, 40, new Color(165 / 255f, 228 / 255f, 1, alpha * 0.88f), 1.15f, true);
            DrawArc(pos, radius * 0.62f, 0.4f, 2.2f, 16, new Color(110 / 255f, 195 / 255f, 1, alpha * 0.45f), 0.8f, true);
        }
    }

    private void DrawStar(Vector2 pos, float size, Color color, Color core, float alpha)
    {
        if (alpha <= 0.02f) return;
        var points = new Vector2[StarShape.Length];
        for (int i = 0; i < points.Length; i++) points[i] = pos + StarShape[i] * size;
        color.A *= Mathf.Clamp(alpha, 0, 1);
        core.A *= Mathf.Clamp(alpha, 0, 1);
        DrawColoredPolygon(points, color);
        DrawCircle(pos, size * 0.24f, core, true, -1, true);
    }

    private static Vector2[] MakeStarShape()
    {
        // Sample the source's four quadratic curves once; scale/translate only at runtime.
        var points = new Vector2[32];
        Vector2 start = new(0, -1.35f), control = new(0.16f, -0.16f), end = new(1.35f, 0);
        for (int side = 0; side < 4; side++)
        for (int i = 0; i < 8; i++)
        {
            float f = i / 8f;
            points[side * 8 + i] = (start * (1 - f) * (1 - f) + control * 2 * (1 - f) * f + end * f * f)
                .Rotated(side * Mathf.Pi * 0.5f);
        }
        return points;
    }
}
