using Godot;

namespace Denia;

/// <summary>黑态四段及以上：每张牌只播一次，前四段卡四次挥击，第五段起不再等待。</summary>
public partial class DeniaDarkFourAttackVisual : DeniaSingleHitVisual
{
    public const string FourNodeName = "RealtimeFourHit";
    public const double Duration = 11.466;
    // 原素材第10／19／26／33帧为四次攻击光效抵达右侧的命中峰值，不按等间隔平均分配。
    public static readonly double[] ImpactTimes = [Duration * 10 / 116, Duration * 19 / 116,
        Duration * 26 / 116, Duration * 33 / 116];
    protected override string Assets => "res://images/characters/denia/realtime_dark_4hit/";
    protected override string ShaderPath => "res://shaders/denia_dark_4hit.gdshader";
    public override double DurationSeconds => Duration;
    public override int FrameCount => 116;
    // 以人物头部至靴尖标定，泡泡、长裙尾与巨大攻击特效不参与缩放标准。
    public override float LayoutHeight => 465;
    public override Vector2 DesignOrigin => new(718, 766);
    protected override Vector2 CanvasSize => new(1536, 864);
    protected override bool UsesTracking => false;
    protected override bool WrapFrames => true;
    public override double DamageTime => ImpactTimes[0];
    public override int ImpactCount => 4;
    public override double GetImpactTime(int index) => ImpactTimes[index];
    // 沿用现有攻击移植规则，不叠加HTML的全画幅环境泡泡／星芒。
}
