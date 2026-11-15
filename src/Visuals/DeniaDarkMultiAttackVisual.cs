using Godot;

namespace Denia;

/// <summary>黑态二／三连击共用的一次双挥击演出；第三段不追加动画等待。</summary>
public partial class DeniaDarkMultiAttackVisual : DeniaSingleHitVisual
{
    public const double Duration = 8.783;
    public const double FirstImpact = Duration * 6 / 72;
    public const double SecondImpact = Duration * 12 / 72;
    protected override string Assets => "res://images/characters/denia/realtime_dark_2hit/";
    protected override string ShaderPath => "res://shaders/denia_dark_2hit.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 72;
    public override float LayoutHeight => 430;
    public override Vector2 DesignOrigin => new(852, 796);
    protected override Vector2 CanvasSize => new(1728, 972);
    protected override bool UsesTracking => false;
    protected override bool WrapFrames => true;
    public override double DamageTime => FirstImpact;
    public override int ImpactCount => 2;
    public override double GetImpactTime(int index) => index == 0 ? FirstImpact : SecondImpact;

    // 不接入全画幅环境光环／星芒，保留角色与攻击链、爪痕、冲击特效。
}
