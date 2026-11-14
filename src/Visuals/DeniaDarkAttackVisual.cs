using Godot;

namespace Denia;

/// <summary>黑态单段巨爪：原96幅宽画幅素材与实时光流、暗泡、星芒和爪痕碎片。</summary>
public partial class DeniaDarkAttackVisual : DeniaSingleHitVisual
{
    public const double Duration = 7.933;
    public const double ImpactTime = Duration * 10 / 96;
    public const float IdleScale = 0.4575f;
    public static readonly Vector2 IdleOffset = new(358.65f, 171.6125f);
    protected override string Assets => "res://images/characters/denia/realtime_dark_1hit/";
    protected override string ShaderPath => "res://shaders/denia_dark_1hit.gdshader";
    public override double DurationSeconds => Duration;
    public override double DamageTime => ImpactTime;
    public override int FrameCount => 96;
    // 只按原待机人物标定，不用960×480特效画布的边界来缩小人物。
    public override float LayoutHeight => DeniaBlackIdleVisual.ReferenceHeight * IdleScale;
    public override Vector2 DesignOrigin => IdleOffset + DeniaBlackIdleVisual.Origin * IdleScale;
    protected override Vector2 CanvasSize => new(960, 480);
    protected override bool WrapFrames => true;
    private float[] _radius = null!, _bubbleAlpha = null!, _slashAlpha = null!, _compX = null!, _compY = null!;

    public override void _Ready()
    {
        var data = GD.Load<Resource>(Assets + "tracking.tres");
        _radius = data.GetMeta("bubble_r_dyn").AsFloat32Array();
        _bubbleAlpha = data.GetMeta("bubble_alpha").AsFloat32Array();
        _slashAlpha = data.GetMeta("slash_alpha").AsFloat32Array();
        _compX = data.GetMeta("comp_x").AsFloat32Array();
        _compY = data.GetMeta("comp_y").AsFloat32Array();
        base._Ready();
    }

    protected override void UpdateFlowParameters()
    {
        FlowMaterial.SetShaderParameter("uTime", (float)Clock);
        FlowMaterial.SetShaderParameter("uBubble", new Vector4(479.89f, Track(BubbleCy), Track(_radius), Mathf.Clamp(Track(_bubbleAlpha), 0, 1)));
        FlowMaterial.SetShaderParameter("uHalo", new Vector2(Track(OrbX), Track(OrbY)));
        FlowMaterial.SetShaderParameter("uSlashAlpha", Mathf.Clamp(Track(_slashAlpha), 0, 1));
    }

    private void Star(Vector2 pos, float size, Color color, float alpha, Color? core = null)
        => DeniaBlackIdleVisual.DrawFourPointStar(this, pos, size, color, core ?? Colors.White, alpha);

    private void MiniBubble(Vector2 pos, float radius, float alpha, bool magenta)
    {
        if (alpha <= 0.03f || radius <= 0.5f) return;
        Color edge = magenta ? new Color(1, 72 / 255f, 168 / 255f, alpha * 0.96f)
            : new Color(75 / 255f, 175 / 255f, 1, alpha * 0.95f);
        Color middle = magenta ? new Color(14 / 255f, 10 / 255f, 48 / 255f, alpha * 0.94f)
            : new Color(10 / 255f, 22 / 255f, 68 / 255f, alpha * 0.92f);
        Color center = magenta ? new Color(42 / 255f, 22 / 255f, 92 / 255f, alpha * 0.88f)
            : new Color(28 / 255f, 62 / 255f, 148 / 255f, alpha * 0.85f);
        // Native concentric fills approximate the source radial gradient.
        DrawCircle(pos, radius, edge, true, -1, true);
        DrawCircle(pos, radius * 0.85f, middle, true, -1, true);
        DrawCircle(pos - Vector2.One * radius * 0.25f, radius * 0.45f, center, true, -1, true);
        DrawArc(pos, radius, 0, Mathf.Tau, 40,
            magenta ? new Color(1, 145 / 255f, 215 / 255f, alpha * 0.92f) : new Color(165 / 255f, 228 / 255f, 1, alpha * 0.88f), 1, true);
        DrawArc(pos, radius * 0.62f, 0.4f, 2.2f, 16,
            magenta ? new Color(1, 175 / 255f, 230 / 255f, alpha * 0.5f) : new Color(110 / 255f, 195 / 255f, 1, alpha * 0.45f), 0.7f, true);
    }

    public override void _Draw()
    {
        if (!Initialized) return;
        float t = (float)Clock;
        DrawSetTransform(new Vector2(Flip ? DesignOrigin.X : -DesignOrigin.X, -DesignOrigin.Y), 0,
            new Vector2(Flip ? -1 : 1, 1));
        Vector2 bubble = new(479.89f, Track(BubbleCy));
        float bAlpha = Mathf.Clamp(Track(_bubbleAlpha), 0, 1), radius = Track(_radius);
        if (bAlpha > 0.02f)
        {
            for (int i = 0; i < 64; i++)
            {
                float a = Mathf.Lerp(0.32f, Mathf.Pi * 1.38f, i / 64f);
                float b = Mathf.Lerp(0.32f, Mathf.Pi * 1.38f, (i + 1) / 64f);
                float g = (1 - Mathf.Cos((a + b) * 0.5f - t * 0.55f)) * 0.5f;
                Color color = g < 0.5f ? new Color(130 / 255f, 225 / 255f, 1, 0.82f).Lerp(new Color(55 / 255f, 155 / 255f, 1, 0.38f), g * 2)
                    : new Color(55 / 255f, 155 / 255f, 1, 0.38f).Lerp(new Color(170 / 255f, 235 / 255f, 1, 0.85f), g * 2 - 1);
                color.A *= bAlpha;
                DrawLine(bubble + Vector2.FromAngle(a) * radius, bubble + Vector2.FromAngle(b) * radius, color, 1.25f, true);
            }
            for (int i = 0; i < Stars.Length; i++)
            {
                var sf = Stars[i];float pulse = 0.55f + 0.45f * Mathf.Sin(t * sf.Speed + sf.Phase);
                Star(bubble + sf.Pos + new Vector2(Mathf.Sin(t * 1.2f + i), Mathf.Cos(t * 1.4f + i)) * 0.7f,
                    sf.Size * (0.75f + 0.35f * pulse), sf.Color, pulse * 0.88f * bAlpha, sf.Core);
            }
        }
        if (t >= 0.34f && t <= 0.88f)
        {
            float p = (t - 0.34f) / 0.54f, alpha = Mathf.Sin(p * Mathf.Pi) * (1 - p * 0.35f);
            Vector2 center = new(479.89f, 348.2f);
            DrawArc(center, 31 + p * 29, 0, Mathf.Tau, 80, new Color(120 / 255f, 220 / 255f, 1, alpha * 0.65f), Mathf.Max(0.001f, 1.6f * (1 - p)), true);
            foreach (var bs in Bursts)
            {
                float dist = 19 + bs.Speed * p;
                Vector2 pos = center + new Vector2(Mathf.Cos(bs.Angle) * dist, Mathf.Sin(bs.Angle) * dist * 0.82f - p * 8.5f);
                float r = bs.Radius * (1 - p * 0.45f);
                if (bs.Star) Star(pos, r * 1.15f, new Color(bs.Magenta ? "ff88d8" : "88e8ff"), alpha * 0.92f);
                else MiniBubble(pos, r, alpha * 0.9f, bs.Magenta);
            }
        }
        if (t >= 0.65f && t <= 1.38f)
            foreach (var sw in Wakes)
            {
                float elapsed = t - (0.66f + sw.Delay);
                if (elapsed <= 0 || elapsed >= 0.62f) continue;
                float p = elapsed / 0.62f, alpha = Mathf.Sin(p * Mathf.Pi) * 0.78f;
                Vector2 pos = sw.Pos + sw.Drift * p - new Vector2(0, Mathf.Sin(p * Mathf.Pi) * 5);
                float r = sw.Radius * (0.65f + 0.35f * Mathf.Sin(p * Mathf.Pi));
                if (sw.Star) Star(pos, r, new Color("9ee8ff"), alpha);
                else MiniBubble(pos, r * 0.9f, alpha, sw.Magenta);
            }
        if (t >= 6.45f && t <= 7.38f)
        {
            float p = (t - 6.45f) / 0.93f, alpha = Mathf.Sin(p * Mathf.Pi) * 0.78f;
            for (int i = 0; i < 3; i++)
            {
                float angle = t * 3.8f + i * Mathf.Tau / 3;
                DrawArc(bubble, Mathf.Lerp(52, 34.5f, p), angle, angle + 1.35f, 24,
                    i % 2 == 0 ? new Color(110 / 255f, 220 / 255f, 1, alpha) : new Color(215 / 255f, 95 / 255f, 245 / 255f, alpha * 0.75f), 1.4f, true);
            }
        }
        float haloPulse = 0.72f + 0.28f * Mathf.Sin(t * 3.4f);
        Star(new Vector2(Track(OrbX), Track(OrbY)), 5.2f * haloPulse, new Color(135 / 255f, 225 / 255f, 1, 0.85f), haloPulse * 0.9f);
        if (t < 0.42f || t > 1.15f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.2f + 0.8f);
            Star(new Vector2(Track(_compX), Track(_compY)), 3 * pulse, new Color(1, 238 / 255f, 175 / 255f, 0.78f), pulse * 0.72f);
        }
        for (int i = 0; i < Bubbles.Length; i++)
        {
            var b = Bubbles[i];float p = ((t * b.Speed + b.Phase) % 2.8f) / 2.8f;
            float alpha = Mathf.Sin(p * Mathf.Pi);
            if (alpha <= 0.05f) continue;
            Vector2 pos = b.Pos + b.Drift * p + new Vector2(Mathf.Sin(t * 2.1f + i) * 1.4f, Track(BubbleCy) - 349.12f);
            MiniBubble(pos, b.Radius * (0.75f + 0.3f * alpha), alpha, b.Magenta);
        }
    }

    private readonly record struct Bubble(Vector2 Pos, float Radius, float Speed, float Phase, Vector2 Drift, bool Magenta);
    private readonly record struct Burst(float Angle, float Speed, float Radius, bool Star, bool Magenta);
    private readonly record struct Wake(Vector2 Pos, Vector2 Drift, float Radius, float Delay, bool Magenta, bool Star);
    private readonly record struct Flare(Vector2 Pos, float Size, float Speed, float Phase, Color Color, Color Core);
    // Original deterministic seed tables from dark_1hit_animator.js.
    private static readonly Bubble[] Bubbles = {
        new(new(528.8f,338.6f),4.8f,0.44f,0,new(3,-10.5f),false),
        new(new(521.5f,346.4f),3.7f,0.5f,1.1f,new(2.2f,-8.5f),true),
        new(new(513.3f,282.3f),2.4f,0.56f,2.3f,new(1.6f,-7),false),
        new(new(441.9f,330.8f),3.2f,0.4f,3.6f,new(-2.6f,-9.5f),false),
        new(new(517.9f,369.3f),2.8f,0.52f,4.8f,new(2.4f,-7.5f),true),
        new(new(456.6f,294.2f),2.6f,0.47f,1.9f,new(-2,-9),false) };
    private static readonly Burst[] Bursts = {
        new(0.18f,42,4.2f,false,false),new(0.65f,54,3.5f,true,true),new(1.12f,39,3.9f,false,true),
        new(1.62f,35,3,true,false),new(2.15f,46,4.4f,false,false),new(2.68f,51,3.3f,true,true),
        new(3.18f,41,3.7f,false,false),new(3.75f,44,2.9f,true,false),new(4.3f,49,4,false,true),
        new(4.85f,38,3.2f,true,false),new(5.42f,55,4.1f,false,false),new(5.9f,42,3.4f,true,true) };
    private static readonly Wake[] Wakes = {
        new(new(495,325),new(65,8),4.5f,0,true,false),new(new(545,342),new(78,12),5.2f,0.04f,true,false),
        new(new(580,295),new(72,-8),4.2f,0.06f,false,true),new(new(635,358),new(82,14),4.8f,0.09f,true,false),
        new(new(685,318),new(88,5),5,0.12f,false,true),new(new(725,368),new(74,10),4.1f,0.15f,true,false),
        new(new(775,335),new(90,8),5.4f,0.18f,true,false),new(new(815,278),new(68,-6),4.4f,0.21f,false,true),
        new(new(845,352),new(75,11),4.2f,0.24f,true,false),new(new(880,305),new(62,6),4.6f,0.26f,false,true) };
    private static readonly Flare[] Stars = {
        new(new(-7.8f,6.4f),4,2.8f,0.2f,new("d8fbff"),Colors.White),
        new(new(-12.4f,5),3,3.4f,1.5f,new("9ee8ff"),Colors.White),
        new(new(-13.3f,-7.3f),2.4f,2.5f,2.7f,new("ffd6ec"),new("fff5fa")),
        new(new(-3.7f,-5),2.6f,3.1f,4.1f,new("ffcae8"),Colors.White) };
}
