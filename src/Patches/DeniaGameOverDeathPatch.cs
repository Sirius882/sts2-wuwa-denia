#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

/// <summary>
/// 非战斗场景的 Game Over 不会创建 NCreature，因此不会经过 DeniaStartDeathAnimPatch。
/// 原版在此路径直接给占位角色 Spine 播放 die；改为在新建的视觉上播放达妮娅的死亡覆盖层。
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), "MoveCreaturesToDifferentLayerAndDisableUi")]
public static class DeniaGameOverDeathPatch
{
    private static readonly AccessTools.FieldRef<NGameOverScreen, RunState> RunStateRef =
        AccessTools.FieldRefAccess<NGameOverScreen, RunState>("_runState");

    private static readonly AccessTools.FieldRef<NGameOverScreen, Control> CreatureContainerRef =
        AccessTools.FieldRefAccess<NGameOverScreen, Control>("_creatureContainer");

    [HarmonyPostfix]
    private static void Postfix(NGameOverScreen __instance)
    {
        try
        {
            Control container = CreatureContainerRef(__instance);
            if (container == null || !GodotObject.IsInstanceValid(container))
                return;

            // 战斗路径：所有 Creature 的 Visuals 都会被 Reparent 进 Game Over 容器。
            // 只处理达妮娅玩家：已有 DeniaFormOverlaySync 时死亡动画会跟着 Visuals 继续播；
            // 绝不能给怪物/其他角色补挂死亡覆盖层。
            if (NCombatRoom.Instance != null)
            {
                foreach (NCreature creatureNode in NCombatRoom.Instance.CreatureNodes)
                {
                    if (creatureNode?.Entity?.Player?.Character is not Denia)
                        continue;

                    NCreatureVisuals? deniaVisuals = creatureNode.Visuals;
                    if (deniaVisuals == null || !GodotObject.IsInstanceValid(deniaVisuals))
                        continue;

                    if (deniaVisuals.GetNodeOrNull<DeniaFormOverlaySync>(DeniaFormPatch.SyncNodeName) != null)
                        continue;

                    // 仅在达妮娅丢失战斗 sync 时兜底补挂。
                    DeniaGameOverDeathOverlay.TryAttachToCreatureVisuals(deniaVisuals);
                }
                return;
            }

            if (NMerchantRoom.Instance != null)
            {
                // 仅有 DeniaMerchSprite 的商店立绘会被接管（见 TryAttachToMerchantVisual）。
                foreach (NMerchantCharacter merchantVisual in container.GetChildren().OfType<NMerchantCharacter>())
                    DeniaGameOverDeathOverlay.TryAttachToMerchantVisual(merchantVisual);
                return;
            }

            // 火堆 / 先古 / 普通事件：按 RunState.Players 顺序新建 NCreatureVisuals。
            RunState runState = RunStateRef(__instance);
            List<NCreatureVisuals> visuals = container.GetChildren().OfType<NCreatureVisuals>().ToList();
            int count = Math.Min(runState.Players.Count, visuals.Count);
            for (int i = 0; i < count; i++)
            {
                if (runState.Players[i].Character is Denia)
                    DeniaGameOverDeathOverlay.TryAttachToCreatureVisuals(visuals[i]);
            }
        }
        catch
        {
            // Game Over 视觉失败不得影响放弃/死亡结算。
        }
    }
}

/// <summary>
/// 不依赖 NCreature 的 Game Over 死亡动画。
/// 使用 Sprite2D 挂在 Node2D 宿主上（TextureRect 在纯 Node2D 下不可见）。
/// </summary>
public partial class DeniaGameOverDeathOverlay : Node2D
{
    private const string NodeName = "DeniaGameOverDeathOverlay";
    private const float TransitionDuration = DeniaFormPatch.FormTransitionDuration;
    private const float PhaseDuration = TransitionDuration / 3f;
    private const float PixelScale = 0.02f;
    private const float MidFullScale = 1.2f;
    private const float WeaponCenterOffsetY = -30f;
    private const float WeaponRotationDuration = 0.1f;
    private const float WeaponRiseDuration = 0.2f;
    private const float WeaponFallDuration = 0.2f;
    private const float WeaponDropPixels = 30f;

    private Sprite2D? _pink;
    private Sprite2D? _mid;
    private Sprite2D? _corpse;
    private Sprite2D? _weapon;
    private Vector2 _baseSize = new(200f, 300f);
    private double _elapsed;

    public static void TryAttachToCreatureVisuals(NCreatureVisuals visuals)
    {
        try
        {
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)
                || visuals.GetNodeOrNull<DeniaGameOverDeathOverlay>(NodeName) != null
                || visuals.GetNodeOrNull<DeniaFormOverlaySync>(DeniaFormPatch.SyncNodeName) != null)
                return;

            Control? bounds = visuals.GetNodeOrNull<Control>("%Bounds")
                              ?? visuals.GetNodeOrNull<Control>("Bounds");
            Vector2 size = bounds != null && GodotObject.IsInstanceValid(bounds) ? bounds.Size : Vector2.Zero;
            if (size.X <= 1f || size.Y <= 1f)
                size = new Vector2(200f, 300f);

            Vector2 center = bounds != null && GodotObject.IsInstanceValid(bounds)
                ? visuals.ToLocal(bounds.GlobalPosition) + size * 0.5f
                : Vector2.Zero;

            // 朝向：优先看 body scale，其次看 Visuals 自身 scale。
            bool flip = false;
            try
            {
                Node2D? body = visuals.GetCurrentBody();
                if (body != null && GodotObject.IsInstanceValid(body))
                    flip = body.Scale.X < 0f;
                else if (visuals.Scale.X < 0f)
                    flip = true;
            }
            catch { }

            TryCreate(visuals, center, size, flip, () => HideCreaturePlaceholder(visuals));
        }
        catch
        {
            // 视觉-only：节点已离树或缺失时保持原版死亡视觉。
        }
    }

    public static void TryAttachToMerchantVisual(NMerchantCharacter merchantVisual)
    {
        try
        {
            if (merchantVisual == null || !GodotObject.IsInstanceValid(merchantVisual)
                || merchantVisual.GetNodeOrNull<DeniaGameOverDeathOverlay>(NodeName) != null)
                return;

            // DeniaMerchantPatch 已将此 sprite 作为达妮娅商店立绘，借其位置与比例保持布局不变。
            Sprite2D? source = merchantVisual.GetNodeOrNull<Sprite2D>("DeniaMerchSprite");
            if (source == null || !GodotObject.IsInstanceValid(source) || source.Texture == null)
                return;

            Vector2 size = source.Texture.GetSize();
            if (size.X <= 1f || size.Y <= 1f)
                return;

            // 用源 sprite 的全局中心，换算到 merchantVisual 本地坐标。
            Vector2 center = merchantVisual.ToLocal(source.GlobalPosition);
            // 把源图缩放折进目标尺寸，使死亡图与商店立绘同高。
            Vector2 effectiveSize = size * source.Scale.Abs();
            bool flip = source.FlipH || source.Scale.X < 0f;

            TryCreate(merchantVisual, center, effectiveSize, flip, () =>
            {
                source.Visible = false;
                foreach (Node child in merchantVisual.GetChildren())
                {
                    if (child is Node2D body && body.GetClass() == "SpineSprite")
                        body.Visible = false;
                }
            });
        }
        catch
        {
            // 商店 Game Over 视觉不能阻断原版放弃流程。
        }
    }

    private static void TryCreate(
        Node2D host,
        Vector2 center,
        Vector2 size,
        bool flipH,
        Action hidePlaceholder)
    {
        DeniaGameOverDeathOverlay overlay = new()
        {
            Name = NodeName,
            Position = center
        };
        if (!overlay.Initialize(size, flipH))
        {
            overlay.QueueFree();
            return;
        }

        host.AddChild(overlay);
        hidePlaceholder();
        overlay.SetProcess(true);
        // 立刻画第一帧，避免等下一帧 _Process 才出现。
        overlay.ApplyFrame(0f);
    }

    private bool Initialize(Vector2 size, bool flipH)
    {
        try
        {
            Texture2D? pinkTexture = ResourceLoader.Load<Texture2D>(DeniaFormPatch.PinkTexPath);
            Texture2D? midTexture = ResourceLoader.Load<Texture2D>(DeniaFormPatch.FormToBlackTexPath);
            Texture2D? corpseTexture = ResourceLoader.Load<Texture2D>(DeniaFormPatch.CorpseTexPath);
            Texture2D? weaponTexture = ResourceLoader.Load<Texture2D>(DeniaFormPatch.WeaponTexPath);
            if (pinkTexture == null || midTexture == null || corpseTexture == null || weaponTexture == null)
                return false;

            _baseSize = size;
            _pink = CreateSprite("DeniaGameOverPink", pinkTexture, flipH);
            _mid = CreateSprite("DeniaGameOverTransition", midTexture, flipH);
            _corpse = CreateSprite("DeniaGameOverCorpse", corpseTexture, flipH);
            _weapon = CreateSprite("DeniaGameOverWeapon", weaponTexture, flipH);

            AddChild(_pink);
            AddChild(_mid);
            AddChild(_corpse);
            AddChild(_weapon);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override void _Process(double delta)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            {
                SetProcess(false);
                return;
            }

            _elapsed += delta;
            float time = Mathf.Min((float)_elapsed, TransitionDuration);
            ApplyFrame(time);
            if (_elapsed >= TransitionDuration)
                SetProcess(false);
        }
        catch
        {
            SetProcess(false);
        }
    }

    private void ApplyFrame(float time)
    {
        if (time < PhaseDuration)
        {
            float u = time / PhaseDuration;
            SetBody(_pink, visible: true, alpha: 1f - u, scale: Mathf.Lerp(1f, PixelScale, u));
            SetBody(_mid, visible: true, alpha: u, scale: Mathf.Lerp(PixelScale, MidFullScale, u));
            SetBody(_corpse, visible: false, alpha: 0f, scale: PixelScale);
        }
        else if (time < PhaseDuration * 2f)
        {
            SetBody(_pink, visible: false, alpha: 0f, scale: PixelScale);
            SetBody(_mid, visible: true, alpha: 1f, scale: MidFullScale);
            SetBody(_corpse, visible: false, alpha: 0f, scale: PixelScale);
        }
        else
        {
            float u = Mathf.Clamp((time - PhaseDuration * 2f) / PhaseDuration, 0f, 1f);
            SetBody(_pink, visible: false, alpha: 0f, scale: PixelScale);
            SetBody(_mid, visible: true, alpha: 1f - u, scale: Mathf.Lerp(MidFullScale, PixelScale, u));
            SetBody(_corpse, visible: true, alpha: u, scale: Mathf.Lerp(PixelScale, 1f, u));
        }

        ApplyWeapon(time);
    }

    private static Sprite2D CreateSprite(string name, Texture2D texture, bool flipH)
    {
        return new Sprite2D
        {
            Name = name,
            Texture = texture,
            Centered = true,
            FlipH = flipH,
            Visible = false
        };
    }

    private void SetBody(Sprite2D? sprite, bool visible, float alpha, float scale)
    {
        if (sprite == null || !GodotObject.IsInstanceValid(sprite) || sprite.Texture == null)
            return;

        Vector2 texSize = sprite.Texture.GetSize();
        if (texSize.X <= 0f || texSize.Y <= 0f)
            return;

        // 将贴图缩放到目标 Bounds 尺寸，再乘动画 scale。
        float fit = Mathf.Min(_baseSize.X / texSize.X, _baseSize.Y / texSize.Y);
        float s = fit * scale;
        sprite.Scale = new Vector2(s, s);
        sprite.Position = Vector2.Zero;
        sprite.Rotation = 0f;
        sprite.Visible = visible;
        sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }

    private void ApplyWeapon(float time)
    {
        if (_weapon == null || !GodotObject.IsInstanceValid(_weapon) || _weapon.Texture == null)
            return;

        float rotation;
        float offsetY;
        if (time <= WeaponRotationDuration)
        {
            rotation = Mathf.DegToRad(30f) * (time / WeaponRotationDuration);
            offsetY = 0f;
        }
        else if (time <= WeaponRotationDuration + WeaponRiseDuration)
        {
            float u = (time - WeaponRotationDuration) / WeaponRiseDuration;
            rotation = Mathf.DegToRad(30f);
            offsetY = -WeaponDropPixels * (1f - (1f - u) * (1f - u));
        }
        else if (time <= WeaponRotationDuration + WeaponRiseDuration + WeaponFallDuration)
        {
            float u = (time - WeaponRotationDuration - WeaponRiseDuration) / WeaponFallDuration;
            rotation = Mathf.DegToRad(30f);
            offsetY = -WeaponDropPixels * (1f - u * u);
        }
        else
        {
            rotation = Mathf.DegToRad(30f);
            offsetY = 0f;
        }

        Vector2 texSize = _weapon.Texture.GetSize();
        if (texSize.X <= 0f || texSize.Y <= 0f)
            return;

        float fit = Mathf.Min(_baseSize.X / texSize.X, _baseSize.Y / texSize.Y);
        _weapon.Scale = new Vector2(fit, fit);

        // 以左下角为 pivot 做脱手旋转：先把原点放到左下，再旋转/位移。
        Vector2 half = texSize * 0.5f * fit;
        Vector2 bottomLeftOffset = new(-half.X, half.Y);
        Vector2 weaponCenter = new(0f, WeaponCenterOffsetY);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        Vector2 leftBottom = weaponCenter + bottomLeftOffset;
        Vector2 centerRel = weaponCenter - leftBottom;
        Vector2 rotatedRel = new(centerRel.X * cos - centerRel.Y * sin, centerRel.X * sin + centerRel.Y * cos);
        _weapon.Position = leftBottom + rotatedRel + new Vector2(0f, offsetY);
        _weapon.Rotation = rotation;
        _weapon.Modulate = Colors.White;
        _weapon.Visible = true;

        try
        {
            // 武器始终置顶。
            MoveChild(_weapon, GetChildCount() - 1);
        }
        catch { }
    }

    private static void HideCreaturePlaceholder(NCreatureVisuals visuals)
    {
        try
        {
            Node2D? normalBody = visuals.GetNodeOrNull<Node2D>("%Visuals");
            Node2D? phobiaBody = visuals.GetNodeOrNull<Node2D>("%PhobiaModeVisuals");
            if (normalBody != null && GodotObject.IsInstanceValid(normalBody))
                normalBody.Visible = false;
            if (phobiaBody != null && GodotObject.IsInstanceValid(phobiaBody))
                phobiaBody.Visible = false;
            Node2D? currentBody = visuals.GetCurrentBody();
            if (currentBody != null && GodotObject.IsInstanceValid(currentBody))
                currentBody.Visible = false;
        }
        catch { }
    }
}
