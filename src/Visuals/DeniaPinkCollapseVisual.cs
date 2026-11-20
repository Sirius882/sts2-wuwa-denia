using Godot;

namespace Denia;

/// <summary>粉态法杖收束素材的独立预览，使用与粉态单段攻击相同的站姿锚点。</summary>
internal partial class DeniaPinkCollapseVisual : DeniaFlowIdleVisual
{
    protected override string Assets => "res://images/characters/denia/realtime_pink_1hit/";
    protected override string ShaderPath => "res://shaders/denia_pink_1hit.gdshader";
    public override double DurationSeconds => 7.189;
    public override int FrameCount => 48;
    public override float LayoutHeight => 350;
    public override Vector2 DesignOrigin => new(375, 467);
    protected override Vector2 CanvasSize => new(744, 540);
    protected override bool UsesTracking => false;
    protected override bool Loop => false;
}
