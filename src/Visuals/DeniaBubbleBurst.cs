using Godot;

namespace Denia;

/// <summary>HTML点击效果的两态泡泡／星芒；速度从原60Hz逐帧位移改为按秒积分。</summary>
public partial class DeniaBubbleBurst : Node2D
{
    private readonly Vector2[] _starts = new Vector2[10], _velocities = new Vector2[10];
    private readonly float[] _radii = new float[10], _lifetimes = new float[10];
    private static readonly Vector2[] PinkStar = [new(0, -1.5f), new(0.35f, -0.35f), new(1.5f, 0), new(0.35f, 0.35f),
        new(0, 1.5f), new(-0.35f, 0.35f), new(-1.5f, 0), new(-0.35f, -0.35f)];
    public bool IsBlack { get; init; }
    public float PlaybackSpeed { get; init; } = 1;
    public double Elapsed { get; private set; }

    public override void _Ready()
    {
        var random = new RandomNumberGenerator();
        random.Randomize(); // 私有表现随机数，不借用战斗RNG。
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.Tau * i / 10 + random.Randf() * 0.35f;
            float speed = IsBlack ? 0.8f + random.Randf() * 2 : 0.9f + random.Randf() * 2.1f;
            float spread = IsBlack ? 16 : 20;
            _starts[i] = new Vector2(random.Randf() - 0.5f, random.Randf() - 0.5f) * spread;
            _velocities[i] = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed - (IsBlack ? 0.4f : 0.5f)) * 60;
            _radii[i] = 3.5f + random.Randf() * (IsBlack ? 6 : 7);
            _lifetimes[i] = 1 + random.Randf() * (IsBlack ? 0.45f : 0.5f);
        }
    }

    public override void _Process(double delta)
    {
        Elapsed += delta * PlaybackSpeed;
        if (Elapsed >= 1.5) QueueFree();
        else QueueRedraw();
    }

    public override void _Draw()
    {
        for (int i = 0; i < 10; i++)
        {
            float ratio = 1 - (float)Elapsed / _lifetimes[i];
            if (ratio <= 0) continue;
            Vector2 p = _starts[i] + _velocities[i] * (float)Elapsed;
            float radius = _radii[i];
            if (i % 2 == 0)
            {
                radius *= IsBlack ? 1.1f - ratio * 0.2f : 1.15f - ratio * 0.25f;
                Color fill = IsBlack ? new Color(14 / 255f, 32 / 255f, 92 / 255f, ratio * 0.85f)
                    : new Color(235 / 255f, 165 / 255f, 1, ratio * 0.28f);
                Color rim = IsBlack ? new Color(135 / 255f, 220 / 255f, 1, ratio * 0.92f)
                    : new Color(250 / 255f, 205 / 255f, 1, ratio * 0.9f);
                DrawCircle(p, radius, fill, true, -1, true);
                DrawArc(p, radius, 0, Mathf.Tau, 36, rim, IsBlack ? 1.2f : 1.3f, true);
            }
            else if (IsBlack)
            {
                DeniaBlackIdleVisual.DrawFourPointStar(this, p, radius * 0.9f * ratio,
                    new Color("7ce0ff"), Colors.White, ratio);
            }
            else
            {
                // 在单位星形上三角剖分，避免寿命末端缩小／平移后的顶点退化。
                // 绘制变换保留原位置、大小和透明度，随后恢复以免影响下个泡泡。
                DrawSetTransform(p, 0, Vector2.One * (radius * 0.75f * ratio));
                DrawColoredPolygon(PinkStar, new Color(1, 225 / 255f, 252 / 255f, ratio));
                DrawSetTransform(Vector2.Zero);
            }
        }
    }
}
