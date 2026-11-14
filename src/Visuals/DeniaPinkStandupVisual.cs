using Godot;

namespace Denia;

/// <summary>原74幅坐泡泡至侧身站立素材；仅作粉态攻击前导，不叠加环境粒子。</summary>
public partial class DeniaPinkStandupVisual : DeniaFlowIdleVisual
{
    public const double Duration = 3.083;
    public const float SeatedHeight = 659.7f;
    public static readonly Vector2 SeatedOrigin = new(243.4f, 689.3f);
    protected override string Assets => "res://images/characters/denia/realtime_pink_standup/";
    protected override string ShaderPath => "res://shaders/denia_pink_standup.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 74;
    public override float LayoutHeight => 635;
    public override Vector2 DesignOrigin => new(247.5f, 750);
    protected override Vector2 CanvasSize => new(504, 768);
    protected override bool UsesTracking => false;
    protected override bool Loop => false;
    protected override void UpdateFlowParameters() => FlowMaterial.SetShaderParameter("uTime", (float)Clock);
}
