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
/// 双形态实时待机／攻击、内部预览与静态死亡形象。
/// 覆盖层挂在 Visuals 下：缩小 debuff 走 Visuals.Scale；左右朝向同步 Body.Scale.X。
/// 旧黑态挥砍／强化特效已停用，由代码攻击动画统一呈现。
/// </summary>
[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class DeniaFormPatch
{
    internal const string SyncNodeName = "DeniaFormOverlaySync";
    private const string PinkNodeName = "DeniaPinkOverlay";
    private const string BlackNodeName = "DeniaBlackOverlay";
    private const string CorpseNodeName = "DeniaCorpseOverlay";

    internal const string PinkTexPath = "res://images/packed/character_select/denia_pink.png";
    internal const string BlackTexPath = "res://images/packed/character_select/denia_black.png";
    /// <summary>能量 UI 大图标兼尸体形态。</summary>
    internal const string CorpseTexPath = "res://images/ui/combat/denia_energy_icon_big.png";
    // 旧挥砍贴图、强化脉冲及其兼容调用入口均已移除。

    // 动画可增加透明留边，人物大小仍按最初 512 像素画布标定。
    internal const float BodyReferenceHeight = 512f;
    public const float PinkDeathDuration = 0f;

    /// <summary>攻击和粉态衔接在 Fast 模式以三倍速播放，待机／泡泡／处决保持原速。</summary>
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



    private static readonly Dictionary<NCreature, TextureRect> _pinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _corpseOverlay = new();
    private static readonly HashSet<NCreature> _dying = new();
    // 同张牌的攻击动画只启动一次，现由 DeniaPinkAttackRule 管理。

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

    internal static DeniaSingleHitVisual? PlaySingleHit(Creature creature, bool multiHit = false, bool fourHit = false, bool tripleHit = false)
    {
        var node = GetCreatureNode(creature);
        if (node == null || IsDying(node) || creature.IsDead) return null;
        EnsureOverlays(node);
        return GetSyncNode(node)?.PlaySingleHit(DeniaFormHelper.IsBlack(creature), multiHit, fourHit, tripleHit);
    }

    internal static Task PlayAnimationPreview(Creature creature, DeniaAnimationPreviewKind kind)
    {
        if (MegaCrit.Sts2.Core.Helpers.NonInteractiveMode.IsActive || creature.Player?.Character is not Denia)
            return Task.CompletedTask;
        var node = GetCreatureNode(creature);
        if (node == null || IsDying(node) || creature.IsDead) return Task.CompletedTask;
        EnsureOverlays(node);
        return GetSyncNode(node)?.PlayAnimationPreview(kind) ?? Task.CompletedTask;
    }

    /// <summary>黑色形态攻击特效：虚质/黯核强化脉冲 0.2s。攻击牌路径下不调用。</summary>

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

        // 场景单帧只用于加载占位；就绪后由实时动画接管，旧动作信号不再驱动显隐。
        var body = new Node2D { Name = "Body" };
        overlay.AddChild(body);
        sprite.Reparent(body, false);
        sprite.Name = "Idle";
        sprite.Visible = true;
        sprite.Play("idle");
        overlay.Texture = null;
        if (nodeName == "PinkVisuals" || nodeName == "DarkVisuals")
        {
            DeniaFlowIdleVisual realtime = nodeName == "PinkVisuals"
                ? new DeniaPinkIdleVisual { VideoClip = "pink_idle", LoadVideoOnReady = !DeniaModConfig.UseCodeAnimation }
                : new DeniaBlackIdleVisual { VideoClip = "dark_idle", LoadVideoOnReady = !DeniaModConfig.UseCodeAnimation };
            realtime.Name = "RealtimeIdle";
            body.AddChild(realtime);
            sprite.Pause();
            if (realtime.Initialized) sprite.Visible = !realtime.HasBody;
            else realtime.QueueFree();
        }
    }

    private static void RebindSync(NCreature nc)
    {
        if (!_pinkOverlay.TryGetValue(nc, out var p) || p == null) return;
        if (!_blackOverlay.TryGetValue(nc, out var b) || b == null) return;
        _corpseOverlay.TryGetValue(nc, out var corpse);
        EnsureSyncNode(nc, p, b, corpse);
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
        TextureRect? corpse)
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
                existing.Bind(nc, pink, black, corpse);
                return;
            }

            var sync = new DeniaFormOverlaySync { Name = SyncNodeName };
            visuals.AddChild(sync);
            sync.Bind(nc, pink, black, corpse);
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

            GetSyncNode(nc)?.SetBaseLayout(localPos, size);
            SyncFacing(nc);
        }
        catch { }
    }

    internal static readonly string[] AttackVisualNames = [DeniaSingleHitVisual.NodeName,
        DeniaPinkMultiAttackVisual.MultiNodeName, DeniaDarkFourAttackVisual.FourNodeName,
        "VideoSingleHit", "VideoMultiHit", "VideoTripleHit", "VideoFourHit"];

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
        var idle = rect.GetNodeOrNull<AnimatedSprite2D>("Body/Idle");
        if (idle != null && GodotObject.IsInstanceValid(idle))
        {
            idle.Position = size * 0.5f;
            idle.Scale = Vector2.One * (size.Y / BodyReferenceHeight);
        }
        Place(rect.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle"));
        foreach (var name in AttackVisualNames) Place(rect.GetNodeOrNull<DeniaSingleHitVisual>(name));

        void Place(DeniaFlowIdleVisual? visual)
        {
            if (!GodotObject.IsInstanceValid(visual)) return;
            visual!.Position = new Vector2(size.X * 0.5f, size.Y);
            visual.Scale = Vector2.One * (size.Y / visual.LayoutHeight);
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
        }
        catch { }
    }

    private static void SetFlip(Dictionary<NCreature, TextureRect> map, NCreature nc, bool flip)
    {
        if (!map.TryGetValue(nc, out var rect) || rect == null || !GodotObject.IsInstanceValid(rect)) return;
        rect.FlipH = flip;
        var idle = rect.GetNodeOrNull<AnimatedSprite2D>("Body/Idle");
        if (idle != null && GodotObject.IsInstanceValid(idle)) idle.FlipH = flip;
        rect.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.SetFacing(flip);
        foreach (var name in AttackVisualNames)
            rect.GetNodeOrNull<DeniaSingleHitVisual>(name)?.SetFacing(flip);
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
                _dying.Remove(k);
            }
        }
        catch { }
    }
}

/// <summary>
/// 战斗立绘：实时待机、命中门控攻击、内部预览与静态死亡。
/// 视觉-only，不改游戏状态；攻击只在命中点等待，预览等完整演出。
/// </summary>
public partial class DeniaFormOverlaySync : Node
{
    private enum AnimState
    {
        Idle,
        Dead
    }


    private NCreature? _creatureNode;
    private TextureRect? _pink;
    private TextureRect? _black;
    private TextureRect? _corpse;

    private Vector2 _baseLocalPos = Vector2.Zero;
    private Vector2 _baseSize = new(200f, 300f);
    private bool _hasBaseLayout;
    private DeniaAnimationPreview? _preview;
    private DeniaSingleHitVisual? _singleHit;
    private bool _attackWasBlack;
    private bool _useCodeAnimation;

    private Vector2 _lastBodyScale = Vector2.One;
    private Vector2 _lastVisualsScale = Vector2.One;
    private Vector2 _lastBoundsSize = Vector2.Zero;
    private double _layoutTimer;

    public float DeathDuration => 0f;

    private AnimState _state = AnimState.Idle;


    public void Bind(
        NCreature creatureNode,
        TextureRect pink,
        TextureRect black,
        TextureRect? corpse)
    {
        _creatureNode = creatureNode;
        _pink = pink;
        _black = black;
        _corpse = corpse;
        _layoutTimer = 0;
        ApplyAnimationSetting();
        SetProcess(true);
    }

    public void SetBaseLayout(Vector2 localPos, Vector2 size)
    {
        _baseLocalPos = localPos;
        _baseSize = size;
        _hasBaseLayout = true;
        ApplyTransforms();
    }

    internal DeniaPinkAttackVisual? PlayPinkSingleHit() => PlaySingleHit(false) as DeniaPinkAttackVisual;

    internal DeniaSingleHitVisual? PlaySingleHit(bool isBlack, bool multiHit = false, bool fourHit = false, bool tripleHit = false)
    {
        var overlay = isBlack ? _black : _pink;
        if (overlay == null || _creatureNode == null || DeniaFormPatch.IsDying(_creatureNode)
            || isBlack != IsCurrentlyBlack() || _state is AnimState.Dead) return null;
        if (_singleHit?.Playing == true && !_singleHit.CanBeInterrupted) return null;
        try
        {
            StopSingleHit();
            _state = AnimState.Idle;
            _attackWasBlack = isBlack;
            ApplyAnimationSetting();
            fourHit = isBlack && fourHit;
            string clip = isBlack
                ? fourHit ? "dark_4hit" : tripleHit ? "dark_3hit" : multiHit ? "dark_2hit" : "dark_1hit"
                : multiHit ? "pink_2hit" : "pink_1hit";
            bool video = !_useCodeAnimation;
            tripleHit = video && isBlack && tripleHit && !fourHit;
            var nodeName = video
                ? fourHit ? "VideoFourHit" : tripleHit ? "VideoTripleHit" : multiHit ? "VideoMultiHit" : "VideoSingleHit"
                : fourHit ? DeniaDarkFourAttackVisual.FourNodeName : multiHit ? DeniaPinkMultiAttackVisual.MultiNodeName : DeniaSingleHitVisual.NodeName;
            string? videoClip = video ? clip : null;
            _singleHit = overlay.GetNodeOrNull<DeniaSingleHitVisual>(nodeName);
            if (!GodotObject.IsInstanceValid(_singleHit))
            {
                _singleHit = isBlack
                    ? fourHit ? new DeniaDarkFourAttackVisual { VideoClip = videoClip }
                        : multiHit ? new DeniaDarkMultiAttackVisual { VideoClip = videoClip }
                        : new DeniaDarkAttackVisual { VideoClip = videoClip }
                    : multiHit ? new DeniaPinkMultiAttackVisual { VideoClip = videoClip }
                        : new DeniaPinkAttackVisual { VideoClip = videoClip };
                _singleHit.Name = nodeName;
                _singleHit.Visible = false;
                overlay.AddChild(_singleHit);
                if (!_singleHit.Initialized)
                {
                    _singleHit.QueueFree();
                    _singleHit = null;
                    return null;
                }
                _singleHit.Finished += () => {
                    // 倒放起身的坐姿端已推进待机相位，恢复同一相位避免回到旧时钟时跳动。
                    if (_singleHit?.LeadInDuration > 0)
                        overlay.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.Seek(_singleHit.IdleHandoffClock);
                    if (_singleHit is { UsingVideo: true } && isBlack && !_useCodeAnimation)
                        overlay.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.Seek(0);
                    ResetBodyAnimation();
                    StopSingleHit();
                };
            }
            if (video && !_singleHit.UsingVideo) return null;
            EnsureParentAllowsOverflow();
            DeniaFormPatch.SyncOverlayLayout(_creatureNode);
            bool fast = DeniaFormPatch.IsFastMode();
            float speed = fast ? 3 : 1;
            if (isBlack || video) _singleHit.Play(speed, holdAtImpact: true);
            else _singleHit.PlayWithPinkStandup(speed,
                overlay.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.Clock ?? 0, holdAtImpact: true);
            SetBodyVisible(overlay, false);
            return _singleHit;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[DeniaSingleHit] Animation skipped: {exception.Message}");
            StopSingleHit();
            ResetBodyAnimation();
            return null;
        }
    }

    private static void SetBodyVisible(TextureRect? overlay, bool visible)
    {
        if (!GodotObject.IsInstanceValid(overlay)) return;
        // 隐藏共同父节点，旧动作／待机的信号不能重新显露身体。
        var body = overlay!.GetNodeOrNull<Node2D>("Body");
        if (body != null) body.Visible = visible;
    }

    private void StopSingleHit()
    {
        if (GodotObject.IsInstanceValid(_singleHit)) _singleHit!.Stop();
        SetBodyVisible(_pink, true);
        SetBodyVisible(_black, true);
    }

    internal async Task PlayAnimationPreview(DeniaAnimationPreviewKind kind)
    {
        if (GodotObject.IsInstanceValid(_preview) || !_hasBaseLayout || _creatureNode == null
            || DeniaFormPatch.IsDying(_creatureNode) || !DeniaAnimationPreview.IsAvailable(kind)) return;
        if (_singleHit?.Playing == true && !_singleHit.CanBeInterrupted) return;
        StopSingleHit();
        var preview = new DeniaAnimationPreview { Name = "AnimationPreview", Kind = kind,
            Speed = DeniaFormPatch.IsFastMode() ? 3 : 1,
            IdleClock = _pink?.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.Clock ?? 0 };
        try
        {
            _preview = preview;
            GetParent().AddChild(preview);
            ApplyTransforms();
            await preview.Completion;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[DeniaAnimationPreview] Preview skipped: {exception.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(preview)) preview.QueueFree();
            if (ReferenceEquals(_preview, preview)) _preview = null;
            if (GodotObject.IsInstanceValid(_creatureNode) && !DeniaFormPatch.IsDying(_creatureNode!))
                DeniaFormPatch.RefreshForCreature(_creatureNode!.Entity);
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_preview)) _preview!.QueueFree();
    }

    private void ApplyAnimationSetting()
    {
        _useCodeAnimation = DeniaModConfig.UseCodeAnimation;
        foreach (var overlay in new[] { _pink, _black })
            overlay?.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle")?.SetVideoPlayback(!_useCodeAnimation);
        ResetBodyAnimation();
    }

    private void ResetBodyAnimation()
    {
        foreach (var overlay in new[] { _pink, _black })
        {
            if (overlay == null || !GodotObject.IsInstanceValid(overlay)) continue;
            var sprite = overlay.GetNodeOrNull<AnimatedSprite2D>("Body/Idle");
            if (sprite != null && GodotObject.IsInstanceValid(sprite))
            {
                sprite.SpeedScale = 1f;
                sprite.Play("idle");
                sprite.Pause();
                var idle = overlay.GetNodeOrNull<DeniaFlowIdleVisual>("Body/RealtimeIdle");
                sprite.Visible = idle?.HasBody != true;
                idle?.Show();
            }
        }
    }

    public void ClearDeadState()
    {
        StopSingleHit();
        ResetBodyAnimation();
        _state = AnimState.Idle;
        try
        {
            if (_corpse != null && GodotObject.IsInstanceValid(_corpse))
                SetRectVisual(_corpse, false, 1f, 1f, 0f);
        }
        catch { }
    }

    public void SetForm(bool isBlack)
    {
        StopSingleHit();
        if (_state == AnimState.Dead) return;
        ResetBodyAnimation();
        _state = AnimState.Idle;
        SetRectVisual(_pink, !isBlack, 1f, 1f, 0f);
        SetRectVisual(_black, isBlack, 1f, 1f, 0f);
        ApplyTransforms();
    }

    public void PlayDeath()
    {
        if (_state == AnimState.Dead) return;
        StopSingleHit();
        ResetBodyAnimation();
        if (_creatureNode != null && GodotObject.IsInstanceValid(_creatureNode))
            DeniaFormPatch.SetDying(_creatureNode, true);
        // 2026-11-15：用户不需要死亡动画，两态现在直接保留静态能量形象。
        _state = AnimState.Dead;
        SetRectVisual(_pink, false, 1f, 1f, 0f);
        SetRectVisual(_black, false, 1f, 1f, 0f);
        SetRectVisual(_corpse, true, 1f, 1f, 0f);
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
                // Game Over 转移 Visuals 后保留静态能量形象。
                if (_state == AnimState.Dead)
                {
                    ApplyTransforms();
                }
                else
                {
                    QueueFree();
                }
                return;
            }

            if (_useCodeAnimation != DeniaModConfig.UseCodeAnimation)
                ApplyAnimationSetting();

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

            ApplyTransforms();
        }
        catch { }
    }

    private void EnsureParentAllowsOverflow()
    {
        try
        {
            foreach (var rect in new[] { _pink, _black, _corpse })
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
        Vector2 pos = _baseLocalPos;

        if (_singleHit?.Playing == true)
        {
            if (IsCurrentlyBlack() != _attackWasBlack || _state is AnimState.Dead) StopSingleHit();
            else SetBodyVisible(_attackWasBlack ? _black : _pink, false);
        }
        ApplyBody(_pink, pos);
        ApplyBody(_black, pos);

        ApplyBody(_corpse, pos);

        if (GodotObject.IsInstanceValid(_preview))
        {
            if (_state is AnimState.Dead || _creatureNode?.Entity.IsDead == true)
                _preview!.QueueFree();
            else
            {
                if (_preview!.ReplacesBody)
                {
                    if (_pink != null) _pink.Visible = false;
                    if (_black != null) _black.Visible = false;
                }
                _preview!.Position = pos + new Vector2(_baseSize.X * 0.5f, _baseSize.Y);
                _preview.Scale = Vector2.One * (_baseSize.Y / _preview.LayoutHeight);
                _preview.SetFacing(_creatureNode?.Body?.Scale.X < 0);
            }
        }

    }

    private void ApplyBody(TextureRect? rect, Vector2 pos)
    {
        if (rect == null || !GodotObject.IsInstanceValid(rect)) return;
        try
        {
            rect.Size = _baseSize;
            rect.Rotation = 0f;

            // 人物比例／朝向由各代码动画自身负责，覆盖层只提供稳定的脚底布局。
            rect.Scale = Vector2.One;
            rect.PivotOffset = Vector2.Zero;
            rect.Position = pos;
        }
        catch { }
    }
}

/// <summary>
/// Death/revive triggers; code attacks begin when AttackCommand declares its hit count.
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
            var player = cardPlay.Player;
            if (player?.Character is not Denia) return;
            var creature = player.Creature;
            if (creature == null) return;

            var node = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (node != null && DeniaFormPatch.IsDying(node))
                return;

            // 粉态等攻击指令确定段数后只启动一种动作；黑态现在共用此入口。
            DeniaPinkAttackRule.Begin(card, creature, true);
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
            // AnimDie 在原方法返回前读取等待时长，先切换为静态能量形象。
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
            // 两态均无死亡动画，直接显示静态能量图。
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

