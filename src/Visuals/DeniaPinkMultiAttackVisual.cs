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

    // 不再叠加原HTML的全画幅环境粒子；角色和两次挥击特效保留在原光流素材内。
}
