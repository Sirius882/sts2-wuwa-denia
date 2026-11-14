using Godot;

namespace Denia;

/// <summary>黑态二／三连击共用的一次双挥击演出；第三段不追加动画等待。</summary>
public partial class DeniaDarkMultiAttackVisual : DeniaSingleHitVisual
{
    public const double Duration = 8.782;
    public const double FirstImpact = Duration * 10 / 108;
    public const double SecondImpact = Duration * 18 / 108;
    protected override string Assets => "res://images/characters/denia/realtime_dark_2hit/";
    protected override string ShaderPath => "res://shaders/denia_dark_2hit.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 108;
    public override float LayoutHeight => 245;
    public override Vector2 DesignOrigin => new(432, 418);
    protected override Vector2 CanvasSize => new(896, 504);
    protected override bool UsesTracking => false;
    protected override bool WrapFrames => true;
    public override double DamageTime => FirstImpact;
    public override int ImpactCount => 2;
    public override double GetImpactTime(int index) => index == 0 ? FirstImpact : SecondImpact;

    // 不接入原HTML的44个全画幅环境光环／星芒，保留角色与攻击链、爪痕、冲击特效。
}
