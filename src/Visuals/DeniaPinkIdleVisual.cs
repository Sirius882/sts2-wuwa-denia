using System;
using Godot;

namespace Denia;

/// <summary>原 JS 的高清光流待机：GPU 连续补间，附属泡泡／晶片由 CPU 实时绘制。</summary>
public partial class DeniaPinkIdleVisual : Node2D
{
    public const double Duration = 10.632;
    public const float ReferenceHeight = 552f;
    public static readonly Vector2 Origin = new(296, 596);
    private const string Assets = "res://images/characters/denia/realtime_pink/";
    private ShaderMaterial _material = null!;
    private ColorRect _body = null!;
    private AnimatedSprite2D? _actions;
    private float[] _bubbleCy = null!, _orbX = null!, _orbY = null!;
    private double _clock;
    private bool _flip;

    // Authored parameters from _drawCrispVectorEnhancements; no gameplay RNG.
    private static readonly float[] Angles = [-0.28f, 0.22f, 0.55f, 2.62f, 3.32f, -0.65f];
    private static readonly float[] Distances = [146, 152, 140, 144, 150, 160];
    private static readonly float[] Radii = [7.2f, 9, 5.8f, 8.2f, 6.8f, 6];
    private static readonly float[] Speeds = [0.44f, 0.36f, 0.52f, 0.39f, 0.47f, 0.41f];
    private static readonly float[] Phases = [0.2f, 1.4f, 2.7f, 3.8f, 5.1f, 6.2f];
    private static readonly float[] Hues = [315, 280, 325, 268, 305, 292];

    public double Clock => _clock;
    public bool Initialized { get; private set; }

    public override void _Ready()
    {
        var tracking = GD.Load<Resource>(Assets + "tracking.tres")
            ?? throw new InvalidOperationException("Pink idle tracking resource is missing");
        _bubbleCy = tracking.GetMeta("bubble_cy").AsFloat32Array();
        _orbX = tracking.GetMeta("orb_x").AsFloat32Array();
        _orbY = tracking.GetMeta("orb_y").AsFloat32Array();
        if (_bubbleCy.Length != 56 || _orbX.Length != 56 || _orbY.Length != 56)
            throw new InvalidOperationException("Pink idle requires 56 tracking samples");
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/denia_pink_idle.gdshader") };
        var frames = GD.Load<Texture2D>(Assets + "frames.webp");
        var flow = GD.Load<Texture2D>(Assets + "flow.png");
        if (_material.Shader == null || frames == null || flow == null)
            throw new InvalidOperationException("Pink idle shader or textures are missing");
        _material.SetShaderParameter("frames", frames);
        _material.SetShaderParameter("flow_fields", flow);
        _body = new ColorRect {
            Name = "FlowBody", Position = -Origin, Size = new Vector2(592, 600),
            MouseFilter = Control.MouseFilterEnum.Ignore, Material = _material, ShowBehindParent = true };
        AddChild(_body);
        Seek(0);
        Initialized = true;
    }

    public void BindActions(AnimatedSprite2D actions)
    {
        _actions = actions;
        actions.AnimationChanged += SyncAction;
        SyncAction();
    }

    private void SyncAction()
    {
        if (_actions == null || !GodotObject.IsInstanceValid(_actions)) return;
        bool idle = _actions.Animation == "idle";
        _actions.Visible = !idle;
        Visible = idle;
        // 原 idle 只保留首帧作场景加载占位，不再运行旧待机帧图集。
        if (idle) _actions.Pause();
    }

    public void SetFacing(bool flip)
    {
        _flip = flip;
        _material.SetShaderParameter("flip_h", flip);
        QueueRedraw();
    }

    public void Seek(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        _clock = ((seconds % Duration) + Duration) % Duration;
        float frame = (float)(_clock / Duration * 55);
        _material.SetShaderParameter("frame_index", (int)frame);
        _material.SetShaderParameter("fraction", frame - (int)frame);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) Seek(_clock + delta);
    }

    private float Track(float[] values)
    {
        float frame = (float)(_clock / Duration * 55);
        int i = (int)frame;
        float f = frame - i;
        float a = values[Math.Max(0, i - 1)], b = values[i];
        float c = values[Math.Min(55, i + 1)], d = values[Math.Min(55, i + 2)];
        return 0.5f * (2 * b + (c - a) * f + (2 * a - 5 * b + 4 * c - d) * f * f
            + (-a + 3 * b - 3 * c + d) * f * f * f);
    }

    public override void _Draw()
    {
        if (_bubbleCy == null) return;
        float t = (float)_clock, cy = Track(_bubbleCy);
        DrawSetTransform(new Vector2(_flip ? Origin.X : -Origin.X, -Origin.Y), 0,
            new Vector2(_flip ? -1 : 1, 1));
        for (int i = 0; i < 6; i++)
        {
            float cycle = ((t * Speeds[i] + Phases[i]) % 3.3f) / 3.3f;
            float alpha = Mathf.Sin(cycle * Mathf.Pi);
            if (alpha <= 0.05f) continue;
            Vector2 p = new(307 + Mathf.Cos(Angles[i]) * Distances[i] + Mathf.Sin(t * 2 + i) * 7,
                cy + Mathf.Sin(Angles[i]) * Distances[i] * 0.55f - cycle * 76 + 18);
            float r = Radii[i] * (0.7f + 0.35f * alpha);
            // HSL colors retain the source's pale, translucent bubble palette.
            DrawCircle(p, r, Hsl(Hues[i] + 24, 0.95f, 0.88f, alpha * 0.84f), true, -1, true);
            DrawCircle(p - Vector2.One * r * 0.10f, r * 0.82f,
                Hsl(Hues[i], 0.88f, 0.82f, alpha * 0.44f), true, -1, true);
            DrawCircle(p - Vector2.One * r * 0.35f, Mathf.Max(1, r * 0.22f),
                new Color(1, 1, 1, alpha * 0.85f), true, -1, true);
        }

        Vector2 orb = new(Track(_orbX), Track(_orbY));
        float glowR = 22 * (1 + Mathf.Sin(t * 3.2f) * 0.10f);
        // Concentric low-alpha rings approximate the source's radial canvas gradient.
        for (int i = 12; i > 0; i--)
            DrawCircle(orb, glowR * i / 12, new Color(0.90f, 0.70f, 1, 0.018f), true, -1, true);

        if (t > 2 && t < 6.7f)
        {
            float env = Smoothstep(2, 2.7f, t) * (1 - Smoothstep(5.9f, 6.7f, t));
            for (int i = 0; i < 3; i++)
            {
                float dx = i == 0 ? -6 : i == 1 ? 7 : -2;
                Vector2 p = new(236 + dx + Mathf.Cos(t * 2.2f + i) * 2,
                    cy - 210 - i * 8 + Mathf.Sin(t * 3.4f + i * 1.2f) * 2.5f);
                Bubble(p, i == 0 ? 9.2f : i == 1 ? 7 : 5, env);
            }
        }

        if (t > 6.8f && t < 8.65f)
        {
            float local = (t - 6.8f) / 1.85f;
            Vector2 p = new(307 - 126.3f - 8 - local * 12, cy + 22 - local * 26);
            if (local < 0.65f) Bubble(p, 10.5f, Mathf.Sin(local / 0.65f * Mathf.Pi));
            else
            {
                float burst = (local - 0.65f) / 0.35f, s = 0.85f + burst * 0.55f;
                Color color = new(1, 0.686f, 0.882f, (1 - burst) * 0.88f);
                DrawCircle(p, 5.5f * s, color, true, -1, true);
                DrawCircle(p + new Vector2(-7, -6) * s, 2.8f * s, color, true, -1, true);
                DrawCircle(p + new Vector2(-2.5f, -9.5f) * s, 2.8f * s, color, true, -1, true);
                DrawCircle(p + new Vector2(3.5f, -9.5f) * s, 2.8f * s, color, true, -1, true);
                DrawCircle(p + new Vector2(7.5f, -5) * s, 2.8f * s, color, true, -1, true);
            }
        }

        for (int i = 0; i < 14; i++)
        {
            float phase = i * 0.75f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (0.85f + i % 3 * 0.32f) * 2.4f + phase);
            if (pulse <= 0.28f) continue;
            Vector2 p = new(350 + Mathf.Sin(i * 2.1f) * 142 + Mathf.Sin(t + phase) * 6,
                290 + Mathf.Cos(i * 1.7f) * 162 + Mathf.Cos(t * 0.9f + phase) * 6);
            float s = (2.4f + i % 3 * 1.3f) * (0.65f + pulse * 0.45f);
            float rot = Mathf.Sin(t + i) * 0.25f;
            Color color = new(i % 2 == 0 ? "f6b8ff" : "b894ff");
            color.A = (pulse - 0.25f) * 0.82f;
            DrawColoredPolygon([
                p + new Vector2(0, -s * 1.45f).Rotated(rot),
                p + new Vector2(s * 0.9f, 0).Rotated(rot),
                p + new Vector2(0, s * 1.45f).Rotated(rot),
                p + new Vector2(-s * 0.9f, 0).Rotated(rot)], color);
        }
    }

    private void Bubble(Vector2 p, float radius, float alpha)
    {
        DrawCircle(p, radius, new Color(0.863f, 0.765f, 1, alpha * 0.15f), true, -1, true);
        DrawArc(p, radius, 0, Mathf.Tau, 32, new Color(0.961f, 0.910f, 1, alpha * 0.65f), 1.1f, true);
        DrawCircle(p - Vector2.One * radius * 0.35f, radius * 0.2f,
            new Color(1, 1, 1, alpha * 0.78f), true, -1, true);
    }

    private static Color Hsl(float hue, float saturation, float lightness, float alpha)
    {
        float value = lightness + saturation * Mathf.Min(lightness, 1 - lightness);
        return Color.FromHsv((hue % 360) / 360, value == 0 ? 0 : 2 * (1 - lightness / value), value, alpha);
    }

    private static float Smoothstep(float from, float to, float value)
    {
        float t = Mathf.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
