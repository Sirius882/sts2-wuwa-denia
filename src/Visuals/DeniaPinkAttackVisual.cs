using System;
using System.Threading.Tasks;
using Godot;

namespace Denia;

/// <summary>粉态单段攻击与收招：原48幅素材的实时光流、法阵、虹彩泡与星光。</summary>
public partial class DeniaPinkAttackVisual : DeniaSingleHitVisual
{
    public const double Duration = 5.166;
    protected override string Assets => "res://images/characters/denia/realtime_pink_1hit/";
    protected override string ShaderPath => "res://shaders/denia_pink_1hit.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 48;
    public override float LayoutHeight => 350;
    public override Vector2 DesignOrigin => new(375, 467);
    protected override Vector2 CanvasSize => new(744, 540);
    protected override bool UsesTracking => false;
    public const double ImpactTime = Duration / 2 - 0.5;
    public override double DamageTime => ImpactTime;
    private static readonly Vector2[] StarShape = [new(0, -1.6f), new(0.32f, -0.32f), new(1.6f, 0), new(0.32f, 0.32f),
        new(0, 1.6f), new(-0.32f, 0.32f), new(-1.6f, 0), new(-0.32f, -0.32f)];
    private static readonly Vector2[] Orbs = [new(520, 185), new(556, 242), new(542, 318), new(575, 288), new(515, 372), new(482, 215)];
    private static readonly float[] Radii = [9.5f, 11.2f, 8.4f, 6.5f, 8.8f, 7.2f];
    private static readonly float[] Hues = [325, 310, 285, 165, 330, 340];
    private static readonly float[] Phases = [0.2f, 1.1f, 2, 3.1f, 4, 5.2f];

    private static float SmoothRange(float start, float end, float value)
    {
        float f = Mathf.Clamp((value - start) / (end - start), 0, 1);
        return f * f * (3 - 2 * f);
    }

    public override void _Draw()
    {
        if (!Initialized) return;
        float t = (float)Clock;
        Vector2 origin = new(Flip ? DesignOrigin.X : -DesignOrigin.X, -DesignOrigin.Y);
        Vector2 facing = new(Flip ? -1 : 1, 1);
        DrawSetTransform(origin, 0, facing);
        if (t >= 1.92f && t <= 2.82f)
        {
            float env = SmoothRange(1.92f, 2.08f, t) * (1 - SmoothRange(2.62f, 2.82f, t));
            DrawSetTransform(origin + new Vector2(382, 438) * facing, 0, facing * new Vector2(1, 0.32f));
            for (int i = 0; i < 12; i++)
            {
                float r = 40 + i * 2;
                float alpha = Mathf.Sin((r - 25) / 37 * Mathf.Pi) * env * 0.35f;
                DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 96, new Color(0.84f, 0.72f, 1, Mathf.Max(0, alpha)), 2, true);
            }
            for (float angle = 0; angle < Mathf.Tau; angle += 20 / 52.08f)
                DrawArc(Vector2.Zero, 52.08f, angle + t * 1.8f, angle + t * 1.8f + 12 / 52.08f,
                    8, new Color(185 / 255f, 225 / 255f, 1, env * 0.65f), 1.6f, true);
            DrawSetTransform(origin, 0, facing);
        }
        if (t < 1.95f || t > 3.1f) return;
        float orbEnv = SmoothRange(1.95f, 2.15f, t) * (1 - SmoothRange(2.75f, 3.1f, t));
        for (int i = 0; i < Orbs.Length; i++)
        {
            Vector2 pos = Orbs[i] + new Vector2(Mathf.Cos(t * 2.2f + Phases[i]) * 5 + (t - 2) * 14,
                Mathf.Sin(t * 2.6f + Phases[i]) * 6 - (t - 2) * 10);
            float radius = Radii[i] * (0.8f + 0.25f * Mathf.Sin(t * 4 + i));
            DrawCircle(pos, radius, Color.FromHsv((Hues[i] + 25) / 360, 0.34f, 0.99f, orbEnv * 0.78f), true, -1, true);
            DrawCircle(pos, radius * 0.88f, Color.FromHsv(Hues[i] / 360, 0.3f, 0.98f, orbEnv * 0.38f), true, -1, true);
            DrawCircle(pos - Vector2.One * radius * 0.3f, radius * 0.48f,
                Color.FromHsv(Hues[i] / 360, 0.08f, 1, orbEnv * 0.28f), true, -1, true);
            DrawCircle(pos - Vector2.One * radius * 0.35f, Mathf.Max(1, radius * 0.22f),
                new Color(1, 1, 1, orbEnv * 0.82f), true, -1, true);
            Vector2 star = pos + new Vector2(i % 2 == 0 ? 16 : -14, i % 3 == 0 ? -14 : 15);
            float size = (3.2f + i % 3 * 1.4f) * orbEnv * (0.6f + 0.4f * Mathf.Sin(t * 6.5f + i * 1.7f));
            if (size < 0.01f) continue;
            DrawSetTransform(origin + star * facing, 0, facing * size);
            DrawColoredPolygon(StarShape, i % 2 == 0 ? new Color(1, 215 / 255f, 245 / 255f, orbEnv * 0.88f)
                : new Color(185 / 255f, 1, 235 / 255f, orbEnv * 0.88f));
            DrawSetTransform(origin, 0, facing);
        }
    }
}
