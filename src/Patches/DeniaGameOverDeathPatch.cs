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
                    DeniaGameOverDeathOverlay.TryAttachToCreatureVisuals(deniaVisuals, DeniaFormHelper.IsBlack(creatureNode.Entity));
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
    private AnimatedSprite2D? _pink;
    private Sprite2D? _corpse;
    private double _elapsed;

    public static void TryAttachToCreatureVisuals(NCreatureVisuals visuals, bool isBlack = false)
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

            TryCreate(visuals, center, size, flip, () => HideCreaturePlaceholder(visuals), isBlack);
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
        Action hidePlaceholder,
        bool isBlack = false)
    {
        DeniaGameOverDeathOverlay overlay = new()
        {
            Name = NodeName,
            Position = center
        };
        if (!overlay.Initialize(size, flipH, isBlack))
        {
            overlay.QueueFree();
            return;
        }

        host.AddChild(overlay);
        hidePlaceholder();
        overlay.SetProcess(!isBlack);
        // 立刻画第一帧，避免等下一帧 _Process 才出现。
        if (!isBlack) overlay._pink!.Play("death");
    }

    private bool Initialize(Vector2 size, bool flipH, bool isBlack)
    {
        try
        {
            Texture2D? corpseTexture = ResourceLoader.Load<Texture2D>(DeniaFormPatch.CorpseTexPath);
            var scene = ResourceLoader.Load<PackedScene>("res://scenes/creature_visuals/denia_pink.tscn");
            if (corpseTexture == null || scene == null) return false;
            var source = scene.Instantiate();
            var frames = source.GetNode<AnimatedSprite2D>("PinkVisuals").SpriteFrames;
            source.Free();
            if (!frames.HasAnimation("death")) return false;

            // 使用 Sprite2D 挂在 Node2D 宿主；粉态共享战斗帧动画及同一像素比例。
            _pink = new AnimatedSprite2D
            {
                Name = "DeniaGameOverPink", SpriteFrames = frames, FlipH = flipH,
                Scale = Vector2.One * (size.Y / DeniaFormPatch.BodyReferenceHeight),
                Animation = "death", Visible = !isBlack
            };
            Vector2 texSize = corpseTexture.GetSize();
            _corpse = new Sprite2D
            {
                Name = "DeniaGameOverCorpse", Texture = corpseTexture, FlipH = flipH,
                Scale = Vector2.One * Mathf.Min(size.X / texSize.X, size.Y / texSize.Y),
                Visible = isBlack
            };
            AddChild(_pink);
            AddChild(_corpse);
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
            if (_elapsed < DeniaFormPatch.PinkDeathDuration) return;
            _pink!.Pause();
            _pink.SetFrameAndProgress(_pink.SpriteFrames.GetFrameCount("death") - 1, 1f);
            SetProcess(false);
        }
        catch
        {
            SetProcess(false);
        }
    }

    private static void HideCreaturePlaceholder(NCreatureVisuals visuals)
    {
        try
        {
            foreach (string name in new[] { "PinkVisuals", "DarkVisuals", "DeniaPinkOverlay", "DeniaBlackOverlay" })
            {
                var body = visuals.GetNodeOrNull<CanvasItem>(name);
                if (body != null) body.Visible = false;
            }
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
