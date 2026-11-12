using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Denia;

/// <summary>
/// 双形态战斗立绘覆盖 + 呼吸/黑攻特效/变形/死亡视觉。
/// 覆盖层挂在 Visuals 下：缩小 debuff 走 Visuals.Scale；左右朝向同步 Body.Scale.X。
/// 术语「黑色形态攻击特效」：denia_black_attack_fx.png，左下角对齐黑立绘中心。
/// </summary>
[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class DeniaFormPatch
{
    internal const string SyncNodeName = "DeniaFormOverlaySync";
    private const string PinkNodeName = "DeniaPinkOverlay";
    private const string BlackNodeName = "DeniaBlackOverlay";
    private const string CorpseNodeName = "DeniaCorpseOverlay";
    private const string BlackFxNodeName = "DeniaBlackAttackFx";

    internal const string PinkTexPath = "res://images/packed/character_select/denia_pink.png";
    internal const string BlackTexPath = "res://images/packed/character_select/denia_black.png";
    /// <summary>能量 UI 大图标兼尸体形态。</summary>
    internal const string CorpseTexPath = "res://images/ui/combat/denia_energy_icon_big.png";
    /// <summary>黑色形态攻击特效（常态透明，左下角对齐黑立绘中心）。</summary>
    internal const string BlackAttackFxTexPath = "res://images/ui/combat/denia_black_attack_fx.png";

    // 动画可增加透明留边，人物大小仍按最初 512 像素画布标定。
    internal const float BodyReferenceHeight = 512f;
    public const float PinkDeathDuration = 2f;
    public const float BlackFxDuration = 0.2f;

    /// <summary>设置里的加速模式（Fast）。Instant 由 Cmd.Wait 自行跳过。</summary>
    internal static bool IsFastMode()
    {
        try
        {
            return SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Fast;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Fast 下黑态特效改为 fire-and-forget（速度仍 0.2s）。</summary>
    public static bool ShouldSkipCombatFxWait() => IsFastMode();

    private static readonly Dictionary<NCreature, TextureRect> _pinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _corpseOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackFxOverlay = new();
    private static readonly HashSet<NCreature> _dying = new();
    // 同一张攻击牌若已播 slash，则强化脉冲跳过（满足 if attack elif enhance）
    /// <summary>本张牌为攻击牌时置位：强化脉冲不得播放（if 攻击 elif 强化）。</summary>
    private static readonly HashSet<Creature> _blackFormAttackCardActive = new();

    public static void Postfix(NCreature __instance)
    {
        try
        {
            var creature = __instance.Entity;
            if (creature == null || !creature.IsPlayer) return;
            if (creature.Player?.Character is not Denia) return;
            if (__instance.Visuals == null || !GodotObject.IsInstanceValid(__instance.Visuals)) return;
            EnsureOverlays(__instance);
            RefreshForCreature(creature);
        }
        catch { }
    }

    internal static bool IsDying(NCreature nc) => nc != null && _dying.Contains(nc);
    internal static float GetDeathDuration(NCreature nc) => GetSyncNode(nc)?.DeathDuration ?? PinkDeathDuration;

    internal static void SetDying(NCreature nc, bool active)
    {
        if (nc == null) return;
        if (active) _dying.Add(nc); else _dying.Remove(nc);
    }

    public static void PlayDeath(Creature creature)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node == null) return;
            EnsureOverlays(node);
            SetDying(node, true);
            GetSyncNode(node)?.PlayDeath();
        }
        catch { }
    }

    public static void PlayRevive(Creature creature)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node != null)
            {
                SetDying(node, false);
                GetSyncNode(node)?.ClearDeadState();
            }
            RefreshForCreature(creature);
        }
        catch { }
    }

    /// <summary>黑色形态攻击特效：攻击挥砍 0.2s。</summary>
    public static void BeginBlackFormCardPlay(Creature creature, bool isAttackCard)
    {
        if (creature == null) return;
        if (isAttackCard) _blackFormAttackCardActive.Add(creature);
        else _blackFormAttackCardActive.Remove(creature);
    }

    public static void EndBlackFormCardPlay(Creature creature)
    {
        if (creature != null) _blackFormAttackCardActive.Remove(creature);
    }

    public static bool IsBlackFormAttackCardActive(Creature creature)
        => creature != null && _blackFormAttackCardActive.Contains(creature);

    public static void PlayAttack(Creature creature, bool isBlack)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node == null || IsDying(node)) return;
            EnsureOverlays(node);
            GetSyncNode(node)?.PlayAttack(isBlack);
        }
        catch { }
    }

    internal static bool TryPlayDodge(NCreature node)
    {
        if (node?.Entity?.Player?.Character is not Denia) return false;
        if (IsDying(node)) return true;
        EnsureOverlays(node);
        return GetSyncNode(node)?.PlayDodge(DeniaFormHelper.IsBlack(node.Entity)) == true;
    }

    public static void PlayBlackAttackFxSlash(Creature creature)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node == null || IsDying(node)) return;
            EnsureOverlays(node);
            GetSyncNode(node)?.PlayBlackFxSlash();
        }
        catch { }
    }

    /// <summary>黑色形态攻击特效：虚质/黯核强化脉冲 0.2s。攻击牌路径下不调用。</summary>
    public static void PlayBlackAttackFxPulse(Creature creature)
    {
        try
        {
            // if 攻击牌：绝不播脉冲
            if (IsBlackFormAttackCardActive(creature)) return;
            var node = GetCreatureNode(creature);
            if (node == null || IsDying(node)) return;
            EnsureOverlays(node);
            GetSyncNode(node)?.PlayBlackFxPulse();
        }
        catch { }
    }

    public static async Task AwaitBlackFx(Creature creature)
    {
        if (ShouldSkipCombatFxWait()) return;
        await Cmd.Wait(BlackFxDuration);
    }

    private static NCreature? GetCreatureNode(Creature creature)
    {
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null) return null;
            var node = room.GetCreatureNode(creature);
            if (node == null || !GodotObject.IsInstanceValid(node)) return null;
            return node;
        }
        catch { return null; }
    }

    private static DeniaFormOverlaySync? GetSyncNode(NCreature nc)
    {
        try
        {
            // 优先挂在 Visuals 上：Game Over 会把 Visuals 从 NCreature 上 Reparent 走。
            if (nc.Visuals != null && GodotObject.IsInstanceValid(nc.Visuals))
            {
                var onVisuals = nc.Visuals.GetNodeOrNull<DeniaFormOverlaySync>(SyncNodeName);
                if (onVisuals != null && GodotObject.IsInstanceValid(onVisuals))
                    return onVisuals;
            }

            // 兼容旧位置（NCreature 上）。
            var sync = nc.GetNodeOrNull<DeniaFormOverlaySync>(SyncNodeName);
            return sync != null && GodotObject.IsInstanceValid(sync) ? sync : null;
        }
        catch { return null; }
    }

    private static void EnsureOverlays(NCreature nc)
    {
        if (_pinkOverlay.TryGetValue(nc, out var existingPink)
            && GodotObject.IsInstanceValid(existingPink)
            && existingPink.IsInsideTree())
        {
            HidePlaceholderBodies(nc.Visuals);
            EnsureCorpseOverlay(nc);
            EnsureBlackFxOverlay(nc);
            RebindSync(nc);
            SyncOverlayLayout(nc);
            return;
        }

        try
        {
            var pinkTex = ResourceLoader.Load<Texture2D>(PinkTexPath);
            var blackTex = ResourceLoader.Load<Texture2D>(BlackTexPath);
            if (pinkTex == null || blackTex == null)
            {
                GD.PrintErr("[Denia] Form overlay textures missing");
                return;
            }

            var visuals = nc.Visuals;
            HidePlaceholderBodies(visuals);

            var pink = MakeOverlay(PinkNodeName, pinkTex);
            var black = MakeOverlay(BlackNodeName, blackTex);
            visuals.AddChild(pink);
            visuals.AddChild(black);
            _pinkOverlay[nc] = pink;
            _blackOverlay[nc] = black;
            AttachIdleAnimation(visuals, pink, "PinkVisuals");
            AttachIdleAnimation(visuals, black, "DarkVisuals");

            EnsureCorpseOverlay(nc);
            EnsureBlackFxOverlay(nc);
            RebindSync(nc);
            SyncOverlayLayout(nc);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] Form overlay load error: {ex.Message}");
        }
    }

    private static void AttachIdleAnimation(NCreatureVisuals visuals, TextureRect overlay, string nodeName)
    {
        var sprite = visuals.GetNodeOrNull<AnimatedSprite2D>(nodeName);
        if (sprite == null || !GodotObject.IsInstanceValid(sprite) || sprite.SpriteFrames == null)
            return;

        // 帧动画继承现有覆盖层的显隐、变形与死亡状态，避免两套驱动互相覆盖。
        sprite.Reparent(overlay, false);
        sprite.Name = "Idle";
        sprite.Visible = true;
        sprite.Play("idle");
        sprite.AnimationFinished += () =>
        {
            if (GodotObject.IsInstanceValid(sprite) && (sprite.Animation == "attack" || sprite.Animation == "dodge"))
            {
                sprite.SpeedScale = 1f;
                sprite.Play("idle");
            }
        };
        overlay.Texture = null;
    }

    private static void RebindSync(NCreature nc)
    {
        if (!_pinkOverlay.TryGetValue(nc, out var p) || p == null) return;
        if (!_blackOverlay.TryGetValue(nc, out var b) || b == null) return;
        _corpseOverlay.TryGetValue(nc, out var corpse);
        _blackFxOverlay.TryGetValue(nc, out var blackFx);
        EnsureSyncNode(nc, p, b, corpse, blackFx);
    }

    private static void EnsureCorpseOverlay(NCreature nc)
    {
        try
        {
            var visuals = nc.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;
            if (_corpseOverlay.TryGetValue(nc, out var existing)
                && existing != null && GodotObject.IsInstanceValid(existing) && existing.IsInsideTree())
                return;
            var corpse = TryMakeOptionalOverlay(CorpseNodeName, CorpseTexPath);
            if (corpse == null) return;
            visuals.AddChild(corpse);
            _corpseOverlay[nc] = corpse;
        }
        catch { }
    }

    private static void EnsureBlackFxOverlay(NCreature nc)
    {
        try
        {
            var visuals = nc.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;
            if (_blackFxOverlay.TryGetValue(nc, out var existing)
                && existing != null && GodotObject.IsInstanceValid(existing) && existing.IsInsideTree())
                return;
            var fx = TryMakeOptionalOverlay(BlackFxNodeName, BlackAttackFxTexPath);
            if (fx == null) return;
            // 左下角为 pivot：PivotOffset 在 Apply 时设为 (0, size.Y)
            fx.Modulate = new Color(1, 1, 1, 0);
            visuals.AddChild(fx);
            visuals.MoveChild(fx, visuals.GetChildCount() - 1);
            _blackFxOverlay[nc] = fx;
        }
        catch { }
    }

    private static TextureRect? TryMakeOptionalOverlay(string name, string path)
    {
        try
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex == null) return null;
            return MakeOverlay(name, tex);
        }
        catch { return null; }
    }

    private static void HidePlaceholderBodies(NCreatureVisuals visuals)
    {
        try
        {
            var body = visuals.GetNodeOrNull<Node2D>("%Visuals");
            var phobia = visuals.GetNodeOrNull<Node2D>("%PhobiaModeVisuals");
            if (body != null && GodotObject.IsInstanceValid(body)) body.Visible = false;
            if (phobia != null && GodotObject.IsInstanceValid(phobia)) phobia.Visible = false;
            var current = visuals.GetCurrentBody();
            if (current != null && GodotObject.IsInstanceValid(current)) current.Visible = false;
        }
        catch { }
    }

    private static void EnsureSyncNode(
        NCreature nc,
        TextureRect pink,
        TextureRect black,
        TextureRect? corpse,
        TextureRect? blackFx)
    {
        try
        {
            var visuals = nc.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;

            // 若旧版本把 sync 挂在 NCreature 上，迁移到 Visuals，避免 Game Over Reparent 后动画中断。
            var legacy = nc.GetNodeOrNull<DeniaFormOverlaySync>(SyncNodeName);
            if (legacy != null && GodotObject.IsInstanceValid(legacy) && legacy.GetParent() != visuals)
            {
                legacy.Reparent(visuals);
            }

            var existing = visuals.GetNodeOrNull<DeniaFormOverlaySync>(SyncNodeName);
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                existing.Bind(nc, pink, black, corpse, blackFx);
                return;
            }

            var sync = new DeniaFormOverlaySync { Name = SyncNodeName };
            visuals.AddChild(sync);
            sync.Bind(nc, pink, black, corpse, blackFx);
        }
        catch { }
    }

    private static Control? GetBoundsNode(NCreatureVisuals visuals)
    {
        try
        {
            var bounds = visuals.GetNodeOrNull<Control>("%Bounds");
            if (bounds != null) return bounds;
            var sc = visuals.GetNodeOrNull<Node>("ScaleContainer");
            if (sc != null)
            {
                var b = sc.GetNodeOrNull<Control>("Bounds") ?? sc.GetNodeOrNull<Control>("%Bounds");
                if (b != null) return b;
            }
            return visuals.GetNodeOrNull<Control>("Bounds");
        }
        catch { return null; }
    }

    internal static void SyncOverlayLayout(NCreature nc)
    {
        try
        {
            if (nc == null || !GodotObject.IsInstanceValid(nc) || nc.Visuals == null) return;
            if (!GodotObject.IsInstanceValid(nc.Visuals)) return;
            var bounds = GetBoundsNode(nc.Visuals);
            if (bounds == null || !GodotObject.IsInstanceValid(bounds)) return;

            var localPos = nc.Visuals.ToLocal(bounds.GlobalPosition);
            var size = bounds.Size;
            if (size.X <= 1f || size.Y <= 1f) size = new Vector2(200f, 300f);

            ApplyBaseLayout(_pinkOverlay, nc, localPos, size);
            ApplyBaseLayout(_blackOverlay, nc, localPos, size);
            ApplyBaseLayout(_corpseOverlay, nc, localPos, size);
            // 黑攻特效：尺寸跟 Bounds，pivot 左下角，位置在 ApplyTransforms 对齐中心
            ApplyBaseLayout(_blackFxOverlay, nc, localPos, size);

            GetSyncNode(nc)?.SetBaseLayout(localPos, size);
            SyncFacing(nc);
        }
        catch { }
    }

    private static void ApplyBaseLayout(
        Dictionary<NCreature, TextureRect> map,
        NCreature nc,
        Vector2 localPos,
        Vector2 size)
    {
        if (!map.TryGetValue(nc, out var rect) || rect == null || !GodotObject.IsInstanceValid(rect))
            return;
        rect.Size = size;
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (GetSyncNode(nc) == null) rect.Position = localPos;
        var idle = rect.GetNodeOrNull<AnimatedSprite2D>("Idle");
        if (idle != null && GodotObject.IsInstanceValid(idle))
        {
            idle.Position = size * 0.5f;
            idle.Scale = Vector2.One * (size.Y / BodyReferenceHeight);
        }
    }

    internal static void SyncFacing(NCreature nc)
    {
        try
        {
            if (nc == null || !GodotObject.IsInstanceValid(nc)) return;
            bool flip = false;
            try
            {
                var body = nc.Body;
                if (body != null && GodotObject.IsInstanceValid(body))
                    flip = body.Scale.X < 0f;
            }
            catch { }
            SetFlip(_pinkOverlay, nc, flip);
            SetFlip(_blackOverlay, nc, flip);
            SetFlip(_corpseOverlay, nc, flip);
            SetFlip(_blackFxOverlay, nc, flip);
        }
        catch { }
    }

    private static void SetFlip(Dictionary<NCreature, TextureRect> map, NCreature nc, bool flip)
    {
        if (!map.TryGetValue(nc, out var rect) || rect == null || !GodotObject.IsInstanceValid(rect)) return;
        rect.FlipH = flip;
        var idle = rect.GetNodeOrNull<AnimatedSprite2D>("Idle");
        if (idle != null && GodotObject.IsInstanceValid(idle)) idle.FlipH = flip;
    }

    private static TextureRect MakeOverlay(string name, Texture2D tex)
    {
        return new TextureRect
        {
            Name = name,
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            Modulate = Colors.White
        };
    }

    public static void RefreshForCreature(Creature creature)
    {
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null) return;
            var node = room.GetCreatureNode(creature);
            if (node == null || !GodotObject.IsInstanceValid(node)) return;
            EnsureOverlays(node);

            if (!IsDying(node))
            {
                bool isBlack = DeniaFormHelper.GetForm(creature) == DeniaForm.Black;
                if (_pinkOverlay.TryGetValue(node, out var current) && current.Visible == isBlack)
                    GetSyncNode(node)?.SetForm(isBlack);
                if (_pinkOverlay.TryGetValue(node, out var pink) && pink != null && GodotObject.IsInstanceValid(pink))
                {
                    pink.Visible = !isBlack;
                    pink.Modulate = Colors.White;
                    pink.Scale = Vector2.One;
                    pink.Rotation = 0f;
                }
                if (_blackOverlay.TryGetValue(node, out var black) && black != null && GodotObject.IsInstanceValid(black))
                {
                    black.Visible = isBlack;
                    black.Modulate = Colors.White;
                    black.Scale = Vector2.One;
                    black.Rotation = 0f;
                }
                if (_blackFxOverlay.TryGetValue(node, out var fx) && fx != null && GodotObject.IsInstanceValid(fx))
                {
                    fx.Visible = true; // 始终挂着，靠 alpha 控制
                    fx.Modulate = new Color(1, 1, 1, 0);
                    fx.Scale = Vector2.One;
                    fx.Rotation = 0f;
                }
                HideTransient(node);
            }

            SyncOverlayLayout(node);
            PruneDeadOverlays();
        }
        catch { }
    }

    private static void HideTransient(NCreature node)
    {
        HideOne(_corpseOverlay, node);
    }

    private static void HideOne(Dictionary<NCreature, TextureRect> map, NCreature node)
    {
        if (!map.TryGetValue(node, out var rect) || rect == null || !GodotObject.IsInstanceValid(rect)) return;
        rect.Visible = false;
        rect.Modulate = Colors.White;
        rect.Scale = Vector2.One;
        rect.Rotation = 0f;
    }

    private static void PruneDeadOverlays()
    {
        try
        {
            var dead = new List<NCreature>();
            foreach (var kv in _pinkOverlay)
                if (!GodotObject.IsInstanceValid(kv.Key)) dead.Add(kv.Key);
            foreach (var k in dead)
            {
                _pinkOverlay.Remove(k);
                _blackOverlay.Remove(k);
                _corpseOverlay.Remove(k);
                _blackFxOverlay.Remove(k);
                _dying.Remove(k);
            }
        }
        catch { }
    }
}

/// <summary>
/// 战斗立绘动画：Idle 呼吸 / 黑攻特效 / 两态死亡。
/// 视觉-only，不改游戏状态。等待由调用方 Cmd.Wait。
/// </summary>
public partial class DeniaFormOverlaySync : Node
{
    private enum AnimState
    {
        Idle,
        BlackFxSlash,
        BlackFxPulse,
        DeathTransition,
        Dead
    }

    private const float BreathPeriod = 4.4f; // 原 2.2s，减半速度 → 周期加倍
    /// <summary>仅竖直方向正弦拉伸幅度（相对 Scale.Y）。</summary>
    private const float BreathScaleAmpY = 0.035f / 3f; // 原 0.035，缩到三分之一

    private const float BlackFxSec = 0.2f;
    private const float BlackFxRotDeg = 90f;


    private NCreature? _creatureNode;
    private TextureRect? _pink;
    private TextureRect? _black;
    private TextureRect? _corpse;
    private TextureRect? _blackFx;

    private Vector2 _baseLocalPos = Vector2.Zero;
    private Vector2 _baseSize = new(200f, 300f);
    private bool _hasBaseLayout;
    private bool _hasIdleFrames;

    private Vector2 _lastBodyScale = Vector2.One;
    private Vector2 _lastVisualsScale = Vector2.One;
    private Vector2 _lastBoundsSize = Vector2.Zero;
    private double _layoutTimer;

    private bool _deathWasBlack;
    public float DeathDuration => _deathWasBlack ? 0f : DeniaFormPatch.PinkDeathDuration;

    private AnimState _state = AnimState.Idle;
    private double _animTime;
    private double _breathPhase;

    private Vector2 _motionOffset = Vector2.Zero;
    private float _motionRot;
    private Vector2 _motionScale = Vector2.One;

    private float _blackFxAlpha;
    private float _blackFxRot;

    public void Bind(
        NCreature creatureNode,
        TextureRect pink,
        TextureRect black,
        TextureRect? corpse,
        TextureRect? blackFx)
    {
        _creatureNode = creatureNode;
        _pink = pink;
        _black = black;
        _corpse = corpse;
        _blackFx = blackFx;
        _hasIdleFrames = pink.GetNodeOrNull<AnimatedSprite2D>("Idle") != null
                         || black.GetNodeOrNull<AnimatedSprite2D>("Idle") != null;
        _layoutTimer = 0;
        SetProcess(true);
    }

    public void SetBaseLayout(Vector2 localPos, Vector2 size)
    {
        _baseLocalPos = localPos;
        _baseSize = size;
        _hasBaseLayout = true;
        ApplyTransforms();
    }

    public void PlayAttack(bool isBlack)
    {
        PlayBodyAnimation(isBlack, "attack");
    }

    public bool PlayDodge(bool isBlack) => PlayBodyAnimation(isBlack, "dodge");

    private bool PlayBodyAnimation(bool isBlack, StringName animation)
    {
        if (_state == AnimState.DeathTransition || _state == AnimState.Dead) return true;
        var sprite = (isBlack ? _black : _pink)?.GetNodeOrNull<AnimatedSprite2D>("Idle");
        if (sprite == null || !GodotObject.IsInstanceValid(sprite)
            || !sprite.SpriteFrames.HasAnimation(animation)) return false;
        ResetBodyAnimation();
        sprite.SpeedScale = DeniaFormPatch.IsFastMode() ? 3f : 1f;
        sprite.Play(animation);
        sprite.SetFrameAndProgress(0, 0);
        return true;
    }

    private void ResetBodyAnimation()
    {
        foreach (var overlay in new[] { _pink, _black })
        {
            if (overlay == null || !GodotObject.IsInstanceValid(overlay)) continue;
            var sprite = overlay.GetNodeOrNull<AnimatedSprite2D>("Idle");
            if (sprite != null && GodotObject.IsInstanceValid(sprite)
                && (sprite.Animation == "attack" || sprite.Animation == "dodge" || sprite.Animation == "death"))
            {
                sprite.SpeedScale = 1f;
                sprite.Play("idle");
            }
        }
    }

    public void ClearDeadState()
    {
        ResetBodyAnimation();
        _state = AnimState.Idle;
        _animTime = 0;
        ResetMotion();
        _blackFxAlpha = 0f;
        _blackFxRot = 0f;
        try
        {
            if (_corpse != null && GodotObject.IsInstanceValid(_corpse))
                SetRectVisual(_corpse, false, 1f, 1f, 0f);
        }
        catch { }
    }

    public void PlayBlackFxSlash()
    {
        if (_state == AnimState.DeathTransition || _state == AnimState.Dead) return;
        if (!IsCurrentlyBlack()) return;
        _state = AnimState.BlackFxSlash;
        _animTime = 0;
        _blackFxAlpha = 0f;
        _blackFxRot = 0f;
    }

    public void PlayBlackFxPulse()
    {
        if (_state == AnimState.DeathTransition || _state == AnimState.Dead) return;
        if (!IsCurrentlyBlack()) return;
        // 攻击挥砍优先，不打断 slash
        try
        {
            if (_creatureNode?.Entity != null
                && DeniaFormPatch.IsBlackFormAttackCardActive(_creatureNode.Entity))
                return;
        }
        catch { }
        // attack slash has priority
        if (_state == AnimState.BlackFxSlash) return;
        _state = AnimState.BlackFxPulse;
        _animTime = 0;
        _blackFxAlpha = 0f;
        _blackFxRot = 0f;
    }

    public void SetForm(bool isBlack)
    {
        if (_state == AnimState.DeathTransition || _state == AnimState.Dead) return;
        ResetBodyAnimation();
        _state = AnimState.Idle;
        _animTime = 0;
        ResetMotion();
        _blackFxAlpha = _blackFxRot = 0f;
        SetRectVisual(_pink, !isBlack, 1f, 1f, 0f);
        SetRectVisual(_black, isBlack, 1f, 1f, 0f);
        ApplyTransforms();
    }

    public void PlayDeath()
    {
        if (_state == AnimState.DeathTransition || _state == AnimState.Dead) return;
        _deathWasBlack = IsCurrentlyBlack(); // 在死亡清除形态 Power 之前记住形态。
        ResetBodyAnimation();
        if (_creatureNode != null && GodotObject.IsInstanceValid(_creatureNode))
            DeniaFormPatch.SetDying(_creatureNode, true);
        // 原粉立绘 → 尸体三段过渡已改为粉态跌倒 / 黑态直接显示能量图。
        _state = _deathWasBlack ? AnimState.Dead : AnimState.DeathTransition;
        _animTime = 0;
        ResetMotion();
        _blackFxAlpha = _blackFxRot = 0f;
        SetRectVisual(_pink, !_deathWasBlack, 1f, 1f, 0f);
        SetRectVisual(_black, false, 1f, 1f, 0f);
        SetRectVisual(_corpse, _deathWasBlack, 1f, 1f, 0f);
        if (!_deathWasBlack)
        {
            var sprite = _pink?.GetNodeOrNull<AnimatedSprite2D>("Idle");
            if (sprite != null && sprite.SpriteFrames.HasAnimation("death"))
            {
                // 死亡动画不受加速模式影响，结束后保留伏地末帧。
                sprite.SpeedScale = 1f;
                sprite.Play("death");
                sprite.SetFrameAndProgress(0, 0);
            }
        }
        EnsureParentAllowsOverflow();
        ApplyTransforms();
    }

    private bool IsCurrentlyBlack()
    {
        try
        {
            if (_creatureNode?.Entity == null) return _black?.Visible == true;
            return DeniaFormHelper.IsBlack(_creatureNode.Entity);
        }
        catch { return false; }
    }

    private void ResetMotion()
    {
        _motionOffset = Vector2.Zero;
        _motionRot = 0f;
        _motionScale = Vector2.One;
    }

    private static void SetRectVisual(TextureRect? rect, bool visible, float alpha, float? scale, float? rotation)
    {
        if (rect == null || !GodotObject.IsInstanceValid(rect)) return;
        rect.Visible = visible;
        var m = rect.Modulate;
        m.A = Mathf.Clamp(alpha, 0f, 1f);
        rect.Modulate = m;
        if (scale.HasValue) rect.Scale = new Vector2(scale.Value, scale.Value);
        if (rotation.HasValue) rect.Rotation = rotation.Value;
    }

    public override void _Process(double delta)
    {
        try
        {
            // 自身必须还挂在有效父节点上；Game Over 会拆走 Visuals，sync 应跟着 Visuals 走。
            if (!GodotObject.IsInstanceValid(this) || GetParent() == null || !IsInsideTree())
            {
                QueueFree();
                return;
            }

            bool creatureValid = _creatureNode != null && GodotObject.IsInstanceValid(_creatureNode);
            if (!creatureValid)
            {
                // 战斗卸载后 NCreature 可能已释放，但死亡/尸体动画仍需继续播完。
                if (_state == AnimState.Dead
                    || _state == AnimState.DeathTransition)
                {
                    UpdateAnimation(delta);
                    ApplyTransforms();
                }
                else
                {
                    QueueFree();
                }
                return;
            }

            bool needFacing = false;
            try
            {
                var body = _creatureNode!.Body;
                if (body != null && GodotObject.IsInstanceValid(body))
                {
                    var bs = body.Scale;
                    if (!bs.IsEqualApprox(_lastBodyScale))
                    {
                        _lastBodyScale = bs;
                        needFacing = true;
                    }
                }
            }
            catch { }
            if (needFacing) DeniaFormPatch.SyncFacing(_creatureNode!);

            bool needLayout = false;
            try
            {
                var visuals = _creatureNode!.Visuals;
                if (visuals != null && GodotObject.IsInstanceValid(visuals))
                {
                    var vs = visuals.Scale;
                    if (!vs.IsEqualApprox(_lastVisualsScale))
                    {
                        _lastVisualsScale = vs;
                        needLayout = true;
                    }
                    var bounds = visuals.GetNodeOrNull<Control>("%Bounds")
                                 ?? visuals.GetNodeOrNull<Control>("Bounds");
                    if (bounds != null && GodotObject.IsInstanceValid(bounds)
                        && !bounds.Size.IsEqualApprox(_lastBoundsSize))
                    {
                        _lastBoundsSize = bounds.Size;
                        needLayout = true;
                    }
                }
            }
            catch { }

            _layoutTimer += delta;
            if (_layoutTimer < 0.5 && ((int)(_layoutTimer * 10) != (int)((_layoutTimer - delta) * 10)))
                needLayout = true;
            if (needLayout) DeniaFormPatch.SyncOverlayLayout(_creatureNode!);

            UpdateAnimation(delta);
            ApplyTransforms();
        }
        catch { }
    }

    private void UpdateAnimation(double delta)
    {
        switch (_state)
        {
            case AnimState.Idle:
                UpdateBreath(delta);
                break;
            case AnimState.BlackFxSlash:
                UpdateBlackFxSlash(delta);
                break;
            case AnimState.BlackFxPulse:
                UpdateBlackFxPulse(delta);
                break;
            case AnimState.DeathTransition:
                UpdateDeath(delta);
                break;
            case AnimState.Dead:
                ResetMotion();
                break;
        }
    }

    private void UpdateBreath(double delta)
    {
        _breathPhase += delta;
        float t = (float)(_breathPhase * (Mathf.Tau / BreathPeriod));
        float sin = Mathf.Sin(t);
        // 仅竖直正弦拉伸；水平不动、不旋转、不位移
        _motionOffset = Vector2.Zero;
        _motionRot = 0f;
        // 帧动画已包含呼吸，不再叠加整张立绘拉伸；静态立绘保留原有呼吸。
        _motionScale = _hasIdleFrames ? Vector2.One : new Vector2(1f, 1f + BreathScaleAmpY * sin);
    }

    private void UpdateBlackFxSlash(double delta)
    {
        ResetMotion();
        _animTime += delta;
        float t = Mathf.Clamp((float)_animTime / BlackFxSec, 0f, 1f);
        if (t < 0.5f) _blackFxAlpha = t / 0.5f;
        else _blackFxAlpha = 1f - (t - 0.5f) / 0.5f;
        _blackFxRot = Mathf.DegToRad(BlackFxRotDeg) * t;
        if (_animTime >= BlackFxSec)
        {
            _blackFxAlpha = 0f;
            _blackFxRot = 0f;
            _state = AnimState.Idle;
        }
    }

    private void UpdateBlackFxPulse(double delta)
    {
        ResetMotion();
        _animTime += delta;
        float t = Mathf.Clamp((float)_animTime / BlackFxSec, 0f, 1f);
        if (t < 0.5f) _blackFxAlpha = t / 0.5f;
        else _blackFxAlpha = 1f - (t - 0.5f) / 0.5f;
        _blackFxRot = 0f;
        if (_animTime >= BlackFxSec)
        {
            _blackFxAlpha = 0f;
            _state = AnimState.Idle;
        }
    }

    private void UpdateDeath(double delta)
    {
        // 死亡动画保持原速；Visuals 转交 Game Over 后仍能完成。
        _animTime += delta;
        ResetMotion();
        if (_animTime < DeniaFormPatch.PinkDeathDuration) return;
        var sprite = _pink?.GetNodeOrNull<AnimatedSprite2D>("Idle");
        if (sprite != null && sprite.SpriteFrames.HasAnimation("death"))
        {
            sprite.Pause();
            sprite.SetFrameAndProgress(sprite.SpriteFrames.GetFrameCount("death") - 1, 1f);
        }
        _state = AnimState.Dead;
    }

    private void EnsureParentAllowsOverflow()
    {
        try
        {
            foreach (var rect in new[] { _pink, _black, _corpse, _blackFx })
            {
                if (rect == null || !GodotObject.IsInstanceValid(rect)) continue;
                rect.ClipContents = false;
                Node? p = rect.GetParent();
                int guard = 0;
                while (p != null && guard++ < 6)
                {
                    if (p is Control ctrl) ctrl.ClipContents = false;
                    p = p.GetParent();
                }
            }
        }
        catch { }
    }

    private void ApplyTransforms()
    {
        if (!_hasBaseLayout) return;
        Vector2 center = _baseLocalPos + _baseSize * 0.5f;
        Vector2 pos = _baseLocalPos + _motionOffset;

        ApplyBody(_pink, pos, centerPivot: true);
        ApplyBody(_black, pos, centerPivot: true);

        ApplyBody(_corpse, pos, centerPivot: true);

        // 黑色形态攻击特效：左下角对齐黑立绘中心
        if (_blackFx != null && GodotObject.IsInstanceValid(_blackFx))
        {
            _blackFx.Size = _baseSize;
            _blackFx.PivotOffset = new Vector2(0f, _baseSize.Y); // 左下角
            Vector2 blackCenter = center + _motionOffset;
            _blackFx.Position = blackCenter - _blackFx.PivotOffset;
            _blackFx.Rotation = _blackFxRot;
            _blackFx.Scale = Vector2.One;
            var m = _blackFx.Modulate;
            m.A = Mathf.Clamp(_blackFxAlpha, 0f, 1f);
            _blackFx.Modulate = m;
            _blackFx.Visible = true;
        }
    }

    private void ApplyBody(TextureRect? rect, Vector2 pos, bool centerPivot)
    {
        if (rect == null || !GodotObject.IsInstanceValid(rect)) return;
        try
        {
            rect.Size = _baseSize;
            rect.Rotation = 0f;

            rect.Scale = _motionScale;
            Vector2 s = rect.Scale;
            // Control.Scale + PivotOffset 不可靠，用位置补偿钉锚点（PivotOffset=0）。
            // 呼吸与死亡共用固定底边，动画本身负责跌倒运动。
            rect.PivotOffset = Vector2.Zero;
            rect.Position = pos + new Vector2(
                _baseSize.X * 0.5f * (1f - s.X),
                _baseSize.Y * (1f - s.Y));
        }
        catch { }
    }
}

/// <summary>
/// Death/revive triggers; black FX via BeforeCardPlayed.
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class DeniaAnimTriggerPatch
{
    public static void Postfix(NCreature __instance, string trigger)
    {
        try
        {
            if (__instance?.Entity == null) return;
            var creature = __instance.Entity;
            if (creature.Player?.Character is not Denia) return;

            if (string.Equals(trigger, "Dead", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trigger, "Die", StringComparison.OrdinalIgnoreCase))
            {
                DeniaFormPatch.PlayDeath(creature);
                return;
            }

            if (string.Equals(trigger, "Revive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trigger, "Idle", StringComparison.OrdinalIgnoreCase))
            {
                if (DeniaFormPatch.IsDying(__instance))
                    DeniaFormPatch.PlayRevive(creature);
                return;
            }

        }
        catch { }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforeCardPlayed))]
public static class DeniaBeforeCardPlayedAnimPatch
{
    public static void Postfix(ref System.Threading.Tasks.Task __result, MegaCrit.Sts2.Core.Combat.ICombatState combatState, MegaCrit.Sts2.Core.Entities.Cards.CardPlay cardPlay)
    {
        _ = combatState;
        if (cardPlay.Card?.Owner == null) return;
        __result = Dispatch(__result, cardPlay);
    }

    private static async System.Threading.Tasks.Task Dispatch(System.Threading.Tasks.Task original, MegaCrit.Sts2.Core.Entities.Cards.CardPlay cardPlay)
    {
        await (original ?? System.Threading.Tasks.Task.CompletedTask);

        try
        {
            var card = cardPlay.Card;
            var player = card.Owner;
            if (player?.Character is not Denia) return;
            var creature = player.Creature;
            if (creature == null) return;

            var node = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (node != null && DeniaFormPatch.IsDying(node))
                return;

            bool isAttack = card.Type == CardType.Attack;
            DeniaFormPatch.BeginBlackFormCardPlay(creature, isAttack);

            bool isBlack = DeniaFormHelper.IsBlack(creature);
            // 身体动作独立播放三秒，不延迟出牌结算或替换既有黑态特效。
            if (isAttack) DeniaFormPatch.PlayAttack(creature, isBlack);
            if (isBlack)
            {
                if (isAttack)
                {
                    DeniaFormPatch.PlayBlackAttackFxSlash(creature);
                    await DeniaFormPatch.AwaitBlackFx(creature);
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class DeniaStartDeathAnimPatch
{
    public static void Prefix(NCreature __instance)
    {
        try
        {
            // AnimDie 在原方法返回前读取等待时长，先启动自定义死亡并记住形态。
            if (__instance?.Entity?.Player?.Character is Denia)
                DeniaFormPatch.PlayDeath(__instance.Entity);
        }
        catch { }
    }

    public static void Postfix(NCreature __instance, bool shouldRemove, ref float __result)
    {
        try
        {
            if (__instance?.Entity?.Player?.Character is not Denia) return;
            // 粉态跌倒 2s，黑态直接显示能量图。
            __result = DeniaFormPatch.GetDeathDuration(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.GetCurrentAnimationTimeRemaining))]
public static class DeniaDeathWaitLengthPatch
{
    public static void Postfix(NCreature __instance, ref float __result)
    {
        try
        {
            if (__instance?.Entity?.Player?.Character is not Denia) return;
            if (!DeniaFormPatch.IsDying(__instance)) return;
            __result = Math.Max(0f, DeniaFormPatch.GetDeathDuration(__instance) - 0.5f); // AnimDie adds 0.5s.
        }
        catch { }
    }
}

