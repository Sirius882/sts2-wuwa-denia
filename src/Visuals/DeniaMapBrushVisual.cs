using Godot;

namespace Denia;

/// <summary>地图画线与慢速落笔泡泡；与战斗点击共用粉态粒子，不占用玩法随机数。</summary>
public partial class DeniaMapBrushVisual : Node2D
{
    public const double EmissionInterval = 0.28;
    private double _untilNext;
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

    public void Advance(double delta, bool drawing, Vector2 pointer, float uiScale)
    {
        if (!drawing) { _untilNext = 0;return; }
        _untilNext -= delta;
        if (_untilNext > 0) return;
        // 卡顿后不补发一大串粒子；停笔后下一次落笔立即发出一组。
        _untilNext = EmissionInterval;
        AddChild(new DeniaBubbleBurst { IsBlack = false, Position = pointer,
            Scale = Vector2.One * uiScale * 0.65f, PlaybackSpeed = 0.55f });
    }
}
