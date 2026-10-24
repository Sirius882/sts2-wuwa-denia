using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

/// <summary>
/// 静态立绘怪物视觉助手：把 denia PCK 内 PNG 包装成 NCreatureVisuals。
/// 节点结构照抄 BaseLib NCreatureVisualsFactory 的 Texture2D 分支（Bounds + Visuals Sprite2D
/// + IntentPos/CenterPos 标记），并按目标高度等比缩放，保证血条/意图/点击区域落位正确。
/// 静态贴图没有 Spine，TriggerAnim 内部空安全，招式动画调用静默跳过。
/// </summary>
public static class DeniaMonsterVisuals
{
    /// <param name="imagePath">res:// 下的 PNG 路径。</param>
    /// <param name="targetHeight">缩放后的逻辑高度（原版普通怪 bounds 约 240×280）。</param>
    public static NCreatureVisuals? Create(string imagePath, float targetHeight)
    {
        Texture2D? texture = GD.Load<Texture2D>(imagePath);
        if (texture == null)
        {
            GD.PushWarning($"[denia] missing monster image: {imagePath}");
            return null;
        }

        Vector2 imgSize = texture.GetSize();
        float scale = targetHeight / imgSize.Y;
        Vector2 boundsSize = new(imgSize.X * scale, targetHeight);

        var visualsNode = new NCreatureVisuals();

        // Bounds：点击/悬停区域，锚在脚底（原点 = 脚底中心）
        var bounds = new Control();
        visualsNode.AddUnique(bounds, "Bounds");
        bounds.Position = new Vector2(-boundsSize.X / 2f, -boundsSize.Y);
        bounds.Size = boundsSize;

        // Visuals：立绘精灵，居中于 bounds
        var sprite = new Sprite2D();
        visualsNode.AddUnique(sprite, "Visuals");
        sprite.Texture = texture;
        sprite.Scale = new Vector2(scale, scale);
        sprite.Position = new Vector2(0f, -targetHeight * 0.5f);

        // IntentPos / CenterPos：照抄工厂 GenerateNode 的相对定位；名字带 '%' 会注册失败。
        var intent = new Marker2D();
        visualsNode.AddUnique(intent, "IntentPos");
        intent.Position = bounds.Position + (bounds.Size * new Vector2(0.5f, 0f)) + new Vector2(0f, -70f);

        var center = new Marker2D();
        visualsNode.AddUnique(center, "CenterPos");
        center.Position = bounds.Position + (bounds.Size * new Vector2(0.5f, 0.6f));

        return visualsNode;
    }
}
