using System;
using System.Collections.Generic;
using Godot;

namespace Denia;

/// <summary>地图画线与连续落笔粒子；与战斗点击共用能量图标和粉态星芒，不占用玩法随机数。</summary>
public partial class DeniaMapBrushVisual : Node2D
{
    private const double EmissionInterval = 1.0 / 6;
    private readonly record struct Particle(Vector2 Origin, Vector2 Velocity, float Radius,
        double SpawnTime, double Lifetime, bool IsStar);

    private readonly List<Particle> _particles = new(16);
    private readonly RandomNumberGenerator _random = new();
    private Texture2D? _energyIcon;
    private Vector2 _lastPointer;
    private double _clock;
    private double _untilNext;
    private bool _wasDrawing;
    private int _emissionIndex;

    internal int ActiveParticleCount => _particles.Count;
    private static Gradient? _gradient;
    public static Gradient BrushGradient => _gradient ??= new Gradient {
        Offsets = [0, 0.2f, 0.45f, 0.72f, 1],
        Colors = [new("ffe0f4"), new("f7addb"), new("d47be3"), new("9555cc"), new("522b91")],
        InterpolationMode = Gradient.InterpolationModeEnum.Cubic,
        InterpolationColorSpace = Gradient.ColorSpace.Oklab };

    public static void Style(Line2D line)
    {
        // 与Aemeath的公开画笔方案一致：只替换绘制线的颜色，橡皮擦材质由游戏保留。
        line.Texture = null;
        line.TextureMode = Line2D.LineTextureMode.None;
        line.Gradient = BrushGradient;
        line.DefaultColor = Colors.White;
        line.Antialiased = true;
    }

    public override void _Ready()
    {
        _energyIcon = GD.Load<Texture2D>(DeniaBubbleBurst.EnergyIconPath);
        _random.Randomize();
    }

    public void Advance(double delta, bool drawing, Vector2 pointer, float uiScale)
    {
        bool needsRedraw = _particles.Count > 0 || drawing;
        _clock += delta;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            if (_clock - particle.SpawnTime >= particle.Lifetime) _particles.RemoveAt(i);
        }

        if (drawing && !_wasDrawing)
        {
            _emissionIndex = 0;
            _untilNext = EmissionInterval;
            Emit(pointer, uiScale, age: 0);
        }
        else if (drawing && delta > 0)
        {
            // 保留帧间余量并沿笔尖轨迹分散发射。卡顿时只补最近的一小段。
            _untilNext -= Math.Min(delta, EmissionInterval);
            if (_untilNext <= 0)
            {
                float fraction = (float)(1 + _untilNext / delta);
                Emit(_lastPointer.Lerp(pointer, fraction), uiScale, age: -_untilNext);
                _untilNext += EmissionInterval;
            }
        }

        _lastPointer = pointer;
        _wasDrawing = drawing;
        if (needsRedraw) QueueRedraw();
    }

    private void Emit(Vector2 pointer, float uiScale, double age)
    {
        // 三颗能量图标之间错开一颗星芒；每次仅从笔尖发射一个粒子。
        bool isStar = _emissionIndex++ % 4 == 3;
        float scale = uiScale * 0.65f;
        var velocity = new Vector2(_random.RandfRange(-50, 50), _random.RandfRange(-75, -18)) * scale;
        _particles.Add(new Particle(pointer, velocity, _random.RandfRange(3.5f, 10.5f) * scale,
            _clock - age, _random.RandfRange(1.8f, 2.6f), isStar));
    }

    public override void _Draw()
    {
        foreach (var particle in _particles)
        {
            double age = _clock - particle.SpawnTime;
            float opacity = 1 - (float)(age / particle.Lifetime);
            Vector2 center = particle.Origin + particle.Velocity * (float)age;
            if (particle.IsStar)
                DeniaBubbleBurst.DrawPinkStar(this, center, particle.Radius, opacity);
            else
                DeniaBubbleBurst.DrawEnergyIcon(this, _energyIcon, center, particle.Radius * (1.15f - opacity * 0.25f), opacity);
        }
    }
}
