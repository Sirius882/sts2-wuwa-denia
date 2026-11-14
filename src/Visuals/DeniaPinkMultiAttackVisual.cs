using Godot;

namespace Denia;

/// <summary>粉态两次挥击及完整收招；前两段等待各自挥击，后续伤害不追加动画等待。</summary>
public partial class DeniaPinkMultiAttackVisual : DeniaSingleHitVisual
{
    public const string MultiNodeName = "RealtimeMultiHit";
    public const double Duration = 14.663333;
    public const double FirstImpact = Duration * 14 / 132;
    public const double SecondImpact = Duration * 20 / 132;
    protected override string Assets => "res://images/characters/denia/realtime_pink_2hit/";
    protected override string ShaderPath => "res://shaders/denia_pink_2hit.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 132;
    public override float LayoutHeight => 227;
    public override Vector2 DesignOrigin => new(418, 416);
    protected override Vector2 CanvasSize => new(896, 504);
    protected override bool UsesTracking => false;
    protected override bool WrapFrames => true;
    public override double DamageTime => FirstImpact;
    public override int ImpactCount => 2;
    public override double GetImpactTime(int index) => index == 0 ? FirstImpact : SecondImpact;

    private readonly record struct Particle(Vector2 Position, float Radius, float Phase, Vector2 Speed);
    private static readonly Particle[] Particles = MakeParticles();
    private static Particle[] MakeParticles()
    {
        // 原素材随机环境粒子采用固定本地种子，不消耗战斗随机数。
        var random = new System.Random(22026);
        float Next() => (float)random.NextDouble();
        var particles = new Particle[42];
        for (int i = 0; i < particles.Length; i++)
            particles[i] = new(new(0.2f + Next() * 0.62f, 0.1f + Next() * 0.78f),
                1.8f + Next() * 4.5f, Next() * Mathf.Tau, new((Next() - 0.48f) * 0.025f, -0.018f - Next() * 0.032f));
        return particles;
    }

    public override void _Draw()
    {
        if (!Initialized) return;
        float t = (float)Clock;
        bool active = t >= 1.15f && t <= 2.85f || t >= 9.5f && t <= 11.95f;
        float motion = t * 0.45f + Mathf.Clamp(t - 1.15f, 0, 1.7f) * 1.1f + Mathf.Clamp(t - 9.5f, 0, 2.45f) * 0.95f;
        DrawSetTransform(new Vector2(Flip ? DesignOrigin.X : -DesignOrigin.X, -DesignOrigin.Y), 0,
            new Vector2(Flip ? -1 : 1, 1));
        for (int i = 0; i < Particles.Length; i++)
        {
            var p = Particles[i];
            float x = p.Position.X + p.Speed.X * motion;
            float y = p.Position.Y + p.Speed.Y * motion;
            y = 0.04f + Mathf.PosMod(y - 0.04f, 0.82f);
            x = 0.92f - Mathf.Abs(Mathf.PosMod(x - 0.08f, 1.68f) - 0.84f);
            Vector2 pos = new(x * 896, y * 504);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.2f + p.Phase);
            float alpha = (0.35f + 0.65f * pulse * pulse) * (active ? 0.65f : 0.22f);
            // 原2倍绘制画布的粒子坐标和半径统一换算至896×504。
            if (i % 4 == 0 && active)
            {
                float r = p.Radius * 1.1f;
                DrawCircle(pos, r, new Color(235 / 255f, 140 / 255f, 1, alpha * 0.525f), true, -1, true);
                DrawCircle(pos, r * 0.75f, new Color(130 / 255f, 235 / 255f, 1, alpha * 0.2625f), true, -1, true);
                DrawCircle(pos - Vector2.One * r * 0.28f, r * 0.35f, new Color(1, 245 / 255f, 1, alpha * 0.6375f), true, -1, true);
                DrawArc(pos, r, 0, Mathf.Tau, 32, Mathf.Sin(t * 3 + p.Phase) > 0
                    ? new Color(1, 185 / 255f, 242 / 255f, alpha * 0.5625f)
                    : new Color(150 / 255f, 245 / 255f, 1, alpha * 0.5625f), 0.7f, true);
            }
            else
            {
                Color color = new(i % 3 == 0 ? "ffa6ec" : i % 3 == 1 ? "8cf6ff" : "ffe6a3");
                DeniaBlackIdleVisual.DrawFourPointStar(this, pos, p.Radius * 1.2f / 1.35f, color, Colors.Transparent, alpha);
            }
        }
    }
}
