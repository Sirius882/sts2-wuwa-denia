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
/// 双形态战斗立绘覆盖 + 呼吸/武器/黑攻特效/变形/死亡视觉。
/// 覆盖层挂在 Visuals 下：缩小 debuff 走 Visuals.Scale；左右朝向同步 Body.Scale.X。
/// 术语「黑色形态攻击特效」：denia_black_attack_fx.png，左下角对齐黑立绘中心。
/// </summary>
[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class DeniaFormPatch
{
    internal const string SyncNodeName = "DeniaFormOverlaySync";
    private const string PinkNodeName = "DeniaPinkOverlay";
    private const string BlackNodeName = "DeniaBlackOverlay";
    private const string MidToBlackNodeName = "DeniaFormToBlackOverlay";
    private const string MidToPinkNodeName = "DeniaFormToPinkOverlay";
    private const string CorpseNodeName = "DeniaCorpseOverlay";
    private const string WeaponNodeName = "DeniaWeaponOverlay";
    private const string BlackFxNodeName = "DeniaBlackAttackFx";

    internal const string PinkTexPath = "res://images/packed/character_select/denia_pink.png";
    internal const string BlackTexPath = "res://images/packed/character_select/denia_black.png";
    internal const string FormToBlackTexPath = "res://images/packed/character_select/denia_form_to_black.png";
    internal const string FormToPinkTexPath = "res://images/packed/character_select/denia_form_to_pink.png";
    internal const string WeaponTexPath = "res://images/packed/character_select/denia_weapon.png";
    /// <summary>能量 UI 大图标兼尸体形态。</summary>
    internal const string CorpseTexPath = "res://images/ui/combat/denia_energy_icon_big.png";
    /// <summary>黑色形态攻击特效（常态透明，左下角对齐黑立绘中心）。</summary>
    internal const string BlackAttackFxTexPath = "res://images/ui/combat/denia_black_attack_fx.png";

    public const float FormTransitionDuration = 1.5f;
    public const float WeaponSwingDuration = 0.2f;
    public const float BlackFxDuration = 0.2f;
    public const float WeaponDeathAnimDuration = 0.5f; // 0.1 rotate + 0.2 up + 0.2 down

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

    /// <summary>
    /// 形态切换阻塞时长：Normal 1.5s；Fast 动画×2 → 等 0.75s 仍等播完。
    /// 死亡不走此 API。
    /// </summary>
    public static float GetFormTransitionWaitDuration()
        => IsFastMode() ? FormTransitionDuration * 0.5f : FormTransitionDuration;

    /// <summary>Fast 下武器/黑态特效改为 fire-and-forget（速度仍 0.2s）。</summary>
    public static bool ShouldSkipCombatFxWait() => IsFastMode();

    private static readonly Dictionary<NCreature, TextureRect> _pinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _midToBlackOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _midToPinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _corpseOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _weaponOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackFxOverlay = new();
    private static readonly HashSet<NCreature> _formTransitioning = new();
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

    internal static bool IsFormTransitioning(NCreature nc) => nc != null && _formTransitioning.Contains(nc);
    internal static bool IsDying(NCreature nc) => nc != null && _dying.Contains(nc);

    internal static void SetFormTransitioning(NCreature nc, bool active)
    {
        if (nc == null) return;
        if (active) _formTransitioning.Add(nc); else _formTransitioning.Remove(nc);
    }

    internal static void SetDying(NCreature nc, bool active)
    {
        if (nc == null) return;
        if (active) _dying.Add(nc); else _dying.Remove(nc);
    }

    public static void EndFormTransition(Creature creature)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node != null) SetFormTransitioning(node, false);
            RefreshForCreature(creature);
        }
        catch { }
    }

    public static void PlayFormTransition(Creature creature, bool toBlack)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node == null) return;
            EnsureOverlays(node);
            GetSyncNode(node)?.PlayFormTransition(toBlack
                ? DeniaFormOverlaySync.TransitionKind.ToBlack
                : DeniaFormOverlaySync.TransitionKind.ToPink);
        }
        catch { }
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
                SetFormTransitioning(node, false);
                GetSyncNode(node)?.ClearDeadState();
            }
            RefreshForCreature(creature);
        }
        catch { }
    }

    /// <summary>粉色：武器挥动。clockwise=true 攻击，false 技能/能力。</summary>
    public static void PlayWeaponSwing(Creature creature, bool clockwise)
    {
        try
        {
            var node = GetCreatureNode(creature);
            if (node == null || IsDying(node)) return;
            EnsureOverlays(node);
            GetSyncNode(node)?.PlayWeaponSwing(clockwise);
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

    public static async Task AwaitWeaponSwing(Creature creature)
    {
        // Fast：视觉仍按 0.2s 播，但不等待（instant 手感）
        if (ShouldSkipCombatFxWait()) return;
        await Cmd.Wait(WeaponSwingDuration);
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
            EnsureMidOverlays(nc);
            EnsureCorpseOverlay(nc);
            EnsureWeaponOverlay(nc);
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

            EnsureMidOverlays(nc);
            EnsureCorpseOverlay(nc);
            EnsureWeaponOverlay(nc);
            EnsureBlackFxOverlay(nc);
            RebindSync(nc);
            SyncOverlayLayout(nc);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] Form overlay load error: {ex.Message}");
        }
    }

    private static void RebindSync(NCreature nc)
    {
        if (!_pinkOverlay.TryGetValue(nc, out var p) || p == null) return;
        if (!_blackOverlay.TryGetValue(nc, out var b) || b == null) return;
        _midToBlackOverlay.TryGetValue(nc, out var mB);
        _midToPinkOverlay.TryGetValue(nc, out var mP);
        _corpseOverlay.TryGetValue(nc, out var corpse);
        _weaponOverlay.TryGetValue(nc, out var weapon);
        _blackFxOverlay.TryGetValue(nc, out var blackFx);
        EnsureSyncNode(nc, p, b, mB, mP, corpse, weapon, blackFx);
    }

    private static void EnsureMidOverlays(NCreature nc)
    {
        try
        {
            var visuals = nc.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;

            if (!_midToBlackOverlay.TryGetValue(nc, out var midBlack)
                || midBlack == null || !GodotObject.IsInstanceValid(midBlack) || !midBlack.IsInsideTree())
            {
                midBlack = TryMakeOptionalOverlay(MidToBlackNodeName, FormToBlackTexPath);
                if (midBlack != null) { visuals.AddChild(midBlack); _midToBlackOverlay[nc] = midBlack; }
            }

            if (!_midToPinkOverlay.TryGetValue(nc, out var midPink)
                || midPink == null || !GodotObject.IsInstanceValid(midPink) || !midPink.IsInsideTree())
            {
                midPink = TryMakeOptionalOverlay(MidToPinkNodeName, FormToPinkTexPath);
                if (midPink != null) { visuals.AddChild(midPink); _midToPinkOverlay[nc] = midPink; }
            }
        }
        catch { }
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

    private static void EnsureWeaponOverlay(NCreature nc)
    {
        try
        {
            var visuals = nc.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;
            if (_weaponOverlay.TryGetValue(nc, out var existing)
                && existing != null && GodotObject.IsInstanceValid(existing) && existing.IsInsideTree())
                return;
            var weapon = TryMakeOptionalOverlay(WeaponNodeName, WeaponTexPath);
            if (weapon == null) return;
            visuals.AddChild(weapon);
            // 武器在粉立绘之上
            visuals.MoveChild(weapon, visuals.GetChildCount() - 1);
            _weaponOverlay[nc] = weapon;
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
        TextureRect? midToBlack,
        TextureRect? midToPink,
        TextureRect? corpse,
        TextureRect? weapon,
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
                existing.Bind(nc, pink, black, midToBlack, midToPink, corpse, weapon, blackFx);
                return;
            }

            var sync = new DeniaFormOverlaySync { Name = SyncNodeName };
            visuals.AddChild(sync);
            sync.Bind(nc, pink, black, midToBlack, midToPink, corpse, weapon, blackFx);
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
            ApplyBaseLayout(_midToBlackOverlay, nc, localPos, size);
            ApplyBaseLayout(_midToPinkOverlay, nc, localPos, size);
            ApplyBaseLayout(_corpseOverlay, nc, localPos, size);
            ApplyBaseLayout(_weaponOverlay, nc, localPos, size);
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
            SetFlip(_midToBlackOverlay, nc, flip);
            SetFlip(_midToPinkOverlay, nc, flip);
            SetFlip(_corpseOverlay, nc, flip);
            SetFlip(_weaponOverlay, nc, flip);
            SetFlip(_blackFxOverlay, nc, flip);
        }
        catch { }
    }

    private static void SetFlip(Dictionary<NCreature, TextureRect> map, NCreature nc, bool flip)
    {
        if (!map.TryGetValue(nc, out var rect) || rect == null || !GodotObject.IsInstanceValid(rect)) return;
        rect.FlipH = flip;
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

            if (!IsFormTransitioning(node) && !IsDying(node))
            {
                bool isBlack = DeniaFormHelper.GetForm(creature) == DeniaForm.Black;
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
                if (_weaponOverlay.TryGetValue(node, out var weapon) && weapon != null && GodotObject.IsInstanceValid(weapon))
                {
                    // 仅粉色显示武器
                    weapon.Visible = !isBlack;
                    weapon.Modulate = Colors.White;
                    weapon.Scale = Vector2.One;
                    weapon.Rotation = 0f;
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
        HideOne(_midToBlackOverlay, node);
        HideOne(_midToPinkOverlay, node);
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
                _midToBlackOverlay.Remove(k);
                _midToPinkOverlay.Remove(k);
                _corpseOverlay.Remove(k);
                _weaponOverlay.Remove(k);
                _blackFxOverlay.Remove(k);
                _formTransitioning.Remove(k);
                _dying.Remove(k);
            }
        }
        catch { }
    }
}


/// <summary>
/// 战斗立绘动画：Idle 呼吸 / 武器挥动 / 黑攻特效 / FormTransition / 死亡。
/// 视觉-only，不改游戏状态。等待由调用方 Cmd.Wait。
/// </summary>
public partial class DeniaFormOverlaySync : Node
{
    public enum TransitionKind { ToBlack, ToPink, ToCorpse }

    private enum AnimState
    {
        Idle,
        WeaponSwing,
        BlackFxSlash,
        BlackFxPulse,
        FormTransition,
        WeaponDeathDrop,
        Dead
    }

    private const float BreathPeriod = 4.4f; // 原 2.2s，减半速度 → 周期加倍
    /// <summary>仅竖直方向正弦拉伸幅度（相对 Scale.Y）。</summary>
    private const float BreathScaleAmpY = 0.035f / 3f; // 原 0.035，缩到三分之一
    /// <summary>武器中心相对角色中心上移像素。</summary>
    private const float WeaponCenterOffsetY = -30f;

    private const float FormPhaseShrinkInSec = 0.5f;
    private const float FormPhaseHoldSec = 0.5f;
    private const float FormPhaseShrinkOutSec = 0.5f;
    private const float FormTransitionSec = FormPhaseShrinkInSec + FormPhaseHoldSec + FormPhaseShrinkOutSec;
    private const float FormPixelScale = 0.02f;
    private const float FormMidFullScale = 2.0f;
    private const float FormMidSlamDistance = 320f;

    private const float WeaponSwingSec = 0.2f;
    private const float WeaponSwingDeg = 30f;
    private const float BlackFxSec = 0.2f;
    private const float BlackFxRotDeg = 90f;
    private const float WeaponDeathRotSec = 0.1f;
    private const float WeaponDeathUpSec = 0.2f;
    private const float WeaponDeathDownSec = 0.2f;
    private const float WeaponDeathDropPx = 30f;
    private const float WeaponDeathG = 10f; // 伪重力，用于上下位移 ease

    private const string FormSfxPhase1 = FmodSfx.block;

    private NCreature? _creatureNode;
    private TextureRect? _pink;
    private TextureRect? _black;
    private TextureRect? _midToBlack;
    private TextureRect? _midToPink;
    private TextureRect? _corpse;
    private TextureRect? _weapon;
    private TextureRect? _blackFx;

    private Vector2 _baseLocalPos = Vector2.Zero;
    private Vector2 _baseSize = new(200f, 300f);
    private bool _hasBaseLayout;

    private Vector2 _lastBodyScale = Vector2.One;
    private Vector2 _lastVisualsScale = Vector2.One;
    private Vector2 _lastBoundsSize = Vector2.Zero;
    private double _layoutTimer;

    private AnimState _state = AnimState.Idle;
    /// <summary>仅粉↔黑形态切换：Fast 下为 2，死亡 ToCorpse 恒为 1。</summary>
    private float _formAnimSpeed = 1f;
    private double _animTime;
    private double _breathPhase;
    private TransitionKind _transitionKind = TransitionKind.ToBlack;
    private bool _weaponClockwise = true;
    private bool _playedPhase1Sfx;
    private bool _playedPhase2Sfx;

    private Vector2 _motionOffset = Vector2.Zero;
    private float _motionRot;
    private Vector2 _motionScale = Vector2.One;
    private Vector2 _midExtraOffset = Vector2.Zero;

    private float _weaponExtraRot;
    private Vector2 _weaponDeathOffset = Vector2.Zero;
    private float _weaponDeathRot;
    private float _blackFxAlpha;
    private float _blackFxRot;

    public void Bind(
        NCreature creatureNode,
        TextureRect pink,
        TextureRect black,
        TextureRect? midToBlack,
        TextureRect? midToPink,
        TextureRect? corpse,
        TextureRect? weapon,
        TextureRect? blackFx)
    {
        _creatureNode = creatureNode;
        _pink = pink;
        _black = black;
        _midToBlack = midToBlack;
        _midToPink = midToPink;
        _corpse = corpse;
        _weapon = weapon;
        _blackFx = blackFx;
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

    public void ClearDeadState()
    {
        _state = AnimState.Idle;
        _animTime = 0;
        ResetMotion();
        _weaponExtraRot = 0f;
        _weaponDeathOffset = Vector2.Zero;
        _weaponDeathRot = 0f;
        _blackFxAlpha = 0f;
        _blackFxRot = 0f;
        try
        {
            if (_corpse != null && GodotObject.IsInstanceValid(_corpse))
                SetRectVisual(_corpse, false, 1f, 1f, 0f);
            if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
            {
                _weapon.Visible = true;
                _weapon.Modulate = Colors.White;
                _weapon.Rotation = 0f;
            }
        }
        catch { }
    }

    public void PlayWeaponSwing(bool clockwise)
    {
        if (_state == AnimState.FormTransition || _state == AnimState.Dead || _state == AnimState.WeaponDeathDrop)
            return;
        // 黑形态不挥武器
        if (IsCurrentlyBlack()) return;
        _weaponClockwise = clockwise;
        _state = AnimState.WeaponSwing;
        _animTime = 0;
        _weaponExtraRot = 0f;
    }

    public void PlayBlackFxSlash()
    {
        if (_state == AnimState.FormTransition || _state == AnimState.Dead) return;
        if (!IsCurrentlyBlack()) return;
        _state = AnimState.BlackFxSlash;
        _animTime = 0;
        _blackFxAlpha = 0f;
        _blackFxRot = 0f;
    }

    public void PlayBlackFxPulse()
    {
        if (_state == AnimState.FormTransition || _state == AnimState.Dead) return;
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

    public void PlayFormTransition(TransitionKind kind)
    {
        if (_creatureNode == null || !GodotObject.IsInstanceValid(_creatureNode)) return;
        _state = AnimState.FormTransition;
        _animTime = 0;
        _transitionKind = kind;
        // Fast：粉↔黑切换视觉 ×2；死亡走 PlayDeath，不经过此路径
        _formAnimSpeed = DeniaFormPatch.IsFastMode() ? 2f : 1f;
        ResetMotion();
        _playedPhase1Sfx = false;
        _playedPhase2Sfx = false;
        _midExtraOffset = Vector2.Zero;
        _weaponExtraRot = 0f;
        DeniaFormPatch.SetFormTransitioning(_creatureNode, true);
        SetupFormTransitionVisibility(kind);
        ApplyTransforms();
    }

    public void PlayDeath()
    {
        if (_creatureNode == null || !GodotObject.IsInstanceValid(_creatureNode)) return;
        DeniaFormPatch.SetDying(_creatureNode, true);
        // 粉立绘 → 尸体（复用 ToCorpse 三段）
        _state = AnimState.FormTransition;
        _animTime = 0;
        _transitionKind = TransitionKind.ToCorpse;
        // 死亡动画不受加速模式影响
        _formAnimSpeed = 1f;
        ResetMotion();
        _playedPhase1Sfx = false;
        _playedPhase2Sfx = false;
        DeniaFormPatch.SetFormTransitioning(_creatureNode, true);
        SetupFormTransitionVisibility(TransitionKind.ToCorpse);
        // 同时启动武器脱手
        _weaponDeathOffset = Vector2.Zero;
        _weaponDeathRot = 0f;
        // 武器死亡动画与尸体变形并行，状态在 Finish 时切到 WeaponDeathDrop/Dead
        ApplyTransforms();
    }

    private bool IsCurrentlyBlack()
    {
        try
        {
            if (_creatureNode?.Entity == null) return false;
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


    private double _weaponDeathTime;
    private bool _weaponDeathActive;

    private void SetupFormTransitionVisibility(TransitionKind kind)
    {
        try
        {
            ResolveTransitionTargets(kind, out var from, out var to, out var mid, out var others);
            SetRectVisual(from, true, 1f, 1f, 0f);
            SetRectVisual(to, false, 0f, FormPixelScale, 0f);
            foreach (var other in others)
                SetRectVisual(other, false, 0f, 1f, 0f);

            if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
            {
                if (kind == TransitionKind.ToBlack)
                {
                    _weapon.Visible = true;
                    _weapon.Modulate = Colors.White;
                }
                else if (kind == TransitionKind.ToPink)
                {
                    // 与粉一起从透明恢复（阶段3）
                    _weapon.Visible = true;
                    _weapon.Modulate = new Color(1, 1, 1, 0);
                }
                else
                {
                    _weapon.Visible = true;
                    _weapon.Modulate = Colors.White;
                    _weaponDeathActive = true;
                    _weaponDeathTime = 0;
                    _weaponDeathOffset = Vector2.Zero;
                    _weaponDeathRot = 0f;
                }
            }

            // 粉↔黑 / 死亡：中间图入场相同——原地小尺寸+透明生成
            SetRectVisual(mid, mid != null, 0f, FormPixelScale, 0f);
            _midExtraOffset = Vector2.Zero;

            if (_blackFx != null && GodotObject.IsInstanceValid(_blackFx))
            {
                _blackFxAlpha = 0f;
                _blackFxRot = 0f;
                _blackFx.Modulate = new Color(1, 1, 1, 0);
            }
        }
        catch { }
    }

    private float GetMidSlamDistance()
    {
        float based = _hasBaseLayout ? _baseSize.Y * FormMidFullScale * 0.9f : FormMidSlamDistance;
        return Mathf.Max(based, FormMidSlamDistance);
    }

    private void ResolveTransitionTargets(
        TransitionKind kind,
        out TextureRect? from,
        out TextureRect? to,
        out TextureRect? mid,
        out List<TextureRect?> others)
    {
        others = new List<TextureRect?>();
        switch (kind)
        {
            case TransitionKind.ToBlack:
                from = _pink; to = _black; mid = _midToBlack;
                others.Add(_midToPink); others.Add(_corpse);
                break;
            case TransitionKind.ToPink:
                from = _black; to = _pink; mid = _midToPink;
                others.Add(_midToBlack); others.Add(_corpse);
                break;
            default:
                from = _pink; to = _corpse; mid = _midToBlack;
                others.Add(_black); others.Add(_midToPink);
                break;
        }
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
                    || _state == AnimState.FormTransition
                    || _state == AnimState.WeaponDeathDrop
                    || _weaponDeathActive)
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
        // 武器死亡脱手可与尸体变形并行
        if (_weaponDeathActive)
            StepWeaponDeath(delta);

        switch (_state)
        {
            case AnimState.Idle:
                UpdateBreath(delta);
                break;
            case AnimState.WeaponSwing:
                UpdateWeaponSwing(delta);
                break;
            case AnimState.BlackFxSlash:
                UpdateBlackFxSlash(delta);
                break;
            case AnimState.BlackFxPulse:
                UpdateBlackFxPulse(delta);
                break;
            case AnimState.FormTransition:
                UpdateFormTransition(delta);
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
        _motionScale = new Vector2(1f, 1f + BreathScaleAmpY * sin);
        if (!_weaponDeathActive)
            _weaponExtraRot = 0f;
    }

    private void UpdateWeaponSwing(double delta)
    {
        UpdateBreath(delta);
        _animTime += delta;
        float half = WeaponSwingSec * 0.5f;
        float sign = _weaponClockwise ? 1f : -1f;
        float peak = Mathf.DegToRad(WeaponSwingDeg) * sign;
        if (_animTime >= WeaponSwingSec)
        {
            _weaponExtraRot = 0f;
            _state = AnimState.Idle;
            return;
        }
        if (_animTime <= half)
            _weaponExtraRot = Mathf.Lerp(0f, peak, (float)(_animTime / half));
        else
            _weaponExtraRot = Mathf.Lerp(peak, 0f, (float)((_animTime - half) / half));
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

    /// <summary>
    /// 武器死亡：0.1s 绕左下角顺时针 30° → 0.2s 上移 30px → 0.2s 下移 30px（重力感 g=10）。
    /// </summary>
    private void StepWeaponDeath(double delta)
    {
        _weaponDeathTime += delta;
        float t = (float)_weaponDeathTime;
        float peakRot = Mathf.DegToRad(30f);

        if (t <= WeaponDeathRotSec)
        {
            float u = t / WeaponDeathRotSec;
            _weaponDeathRot = peakRot * u;
            _weaponDeathOffset = Vector2.Zero;
        }
        else if (t <= WeaponDeathRotSec + WeaponDeathUpSec)
        {
            float u = (t - WeaponDeathRotSec) / WeaponDeathUpSec;
            // 重力减速上抛：位移 = h * (1 - (1-u)^2) 的反向，用 ease-out 近似
            // s = 0.5*g*t^2 归一化：上移阶段速度先快后慢 → easeOutQuad
            float ease = 1f - (1f - u) * (1f - u);
            // 掺 g=10 的手感：u' = clamp(g*u*u,0,1) 混合
            float gBlend = Mathf.Clamp(WeaponDeathG * u * u / 10f, 0f, 1f);
            float k = Mathf.Lerp(ease, gBlend, 0.35f);
            _weaponDeathRot = peakRot;
            _weaponDeathOffset = new Vector2(0f, -WeaponDeathDropPx * k);
        }
        else if (t <= WeaponDeathRotSec + WeaponDeathUpSec + WeaponDeathDownSec)
        {
            float u = (t - WeaponDeathRotSec - WeaponDeathUpSec) / WeaponDeathDownSec;
            // 下落加速：easeInQuad + g
            float ease = u * u;
            float gBlend = Mathf.Clamp(WeaponDeathG * u * u / 10f, 0f, 1f);
            float k = Mathf.Lerp(ease, gBlend, 0.5f);
            _weaponDeathRot = peakRot;
            _weaponDeathOffset = new Vector2(0f, -WeaponDeathDropPx * (1f - k));
        }
        else
        {
            // 落地后停在地上，保持可见；旋转保持 30°
            _weaponDeathRot = peakRot;
            _weaponDeathOffset = Vector2.Zero;
            _weaponDeathActive = false;
            if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
            {
                _weapon.Visible = true;
                // 确保盖在尸体之上
                try
                {
                    var parent = _weapon.GetParent();
                    if (parent != null)
                        parent.MoveChild(_weapon, parent.GetChildCount() - 1);
                }
                catch { }
            }
        }
    }

    private void UpdateFormTransition(double delta)
    {
        // Fast 粉↔黑：_formAnimSpeed=2；死亡 ToCorpse 恒为 1
        _animTime += delta * _formAnimSpeed;
        float time = (float)_animTime;
        ResolveTransitionTargets(_transitionKind, out var from, out var to, out var mid, out _);
        bool hasMid = mid != null && GodotObject.IsInstanceValid(mid) && mid.Texture != null;

        ResetMotion();
        EnsureParentAllowsOverflow();

        try
        {
            if (!_playedPhase1Sfx)
            {
                PlayFormSfxPhase1();
                _playedPhase1Sfx = true;
            }

            if (!hasMid)
            {
                float u = Mathf.Clamp(time / FormTransitionSec, 0f, 1f);
                SetRectVisual(from, true, 1f - u, Mathf.Lerp(1f, FormPixelScale, u), 0f);
                SetRectVisual(to, true, u, Mathf.Lerp(FormPixelScale, 1f, u), 0f);
                SyncWeaponWithFormAlpha(kindFromAlpha: 1f - u, kindToAlpha: u);
                if (!_playedPhase2Sfx && u >= 0.5f)
                {
                    PlayFormSfxPhase2();
                    _playedPhase2Sfx = true;
                }
            }
            else if (time < FormPhaseShrinkInSec)
            {
                float u = Mathf.Clamp(time / FormPhaseShrinkInSec, 0f, 1f);
                float fromScale = Mathf.Lerp(1f, FormPixelScale, u);
                SetRectVisual(from, true, 1f - u, fromScale, 0f);
                SetRectVisual(to, false, 0f, FormPixelScale, 0f);

                // 中间图入场：粉↔黑相同——原地放大 + 变不透明
                float midScale = Mathf.Lerp(FormPixelScale, FormMidFullScale, u);
                SetRectVisual(mid, true, u, midScale, 0f);
                _midExtraOffset = Vector2.Zero;
                if (_transitionKind == TransitionKind.ToPink)
                {
                    // 武器仍藏着，阶段3再随粉出现
                    SyncWeaponWithFormAlpha(kindFromAlpha: 0f, kindToAlpha: 0f);
                }
                else if (_transitionKind == TransitionKind.ToBlack)
                {
                    // 粉→黑：武器与粉一起缩虚化
                    SyncWeaponWithFormAlpha(kindFromAlpha: 1f - u, kindToAlpha: 0f);
                }
                // ToCorpse: 武器不随粉缩放
            }
            else if (time < FormPhaseShrinkInSec + FormPhaseHoldSec)
            {
                if (!_playedPhase2Sfx)
                {
                    PlayFormSfxPhase2();
                    _playedPhase2Sfx = true;
                }
                _midExtraOffset = Vector2.Zero;
                SetRectVisual(from, false, 0f, FormPixelScale, 0f);
                SetRectVisual(mid, true, 1f, FormMidFullScale, 0f);
                SetRectVisual(to, false, 0f, FormPixelScale, 0f);
                if (_transitionKind == TransitionKind.ToBlack)
                    SyncWeaponWithFormAlpha(0f, 0f);
            }
            else
            {
                float u = Mathf.Clamp(
                    (time - FormPhaseShrinkInSec - FormPhaseHoldSec) / FormPhaseShrinkOutSec,
                    0f, 1f);
                float toScale = Mathf.Lerp(FormPixelScale, 1f, u);
                SetRectVisual(from, false, 0f, FormPixelScale, 0f);
                SetRectVisual(to, true, u, toScale, 0f);

                if (_transitionKind == TransitionKind.ToPink)
                {
                    // 黑→粉第三步：中间图竖直向上飞走，不缩小、不透明化
                    float fly = GetMidSlamDistance() * u * 1.2f;
                    _midExtraOffset = new Vector2(0f, -fly);
                    SetRectVisual(mid, true, 1f, FormMidFullScale, 0f);
                    SyncWeaponWithFormAlpha(0f, u);
                }
                else
                {
                    // 粉→黑 / 死亡：中间图仍缩虚化
                    float midScale = Mathf.Lerp(FormMidFullScale, FormPixelScale, u);
                    _midExtraOffset = Vector2.Zero;
                    SetRectVisual(mid, true, 1f - u, midScale, 0f);
                    if (_transitionKind == TransitionKind.ToBlack)
                        SyncWeaponWithFormAlpha(0f, 0f);
                }
            }
        }
        catch { }

        if (_animTime >= FormTransitionSec)
            FinishFormTransition();
    }

    private void SyncWeaponWithFormAlpha(float kindFromAlpha, float kindToAlpha)
    {
        if (_weaponDeathActive) return; // 死亡脱手中不改
        if (_weapon == null || !GodotObject.IsInstanceValid(_weapon)) return;
        float a = Mathf.Max(kindFromAlpha, kindToAlpha);
        // 黑形态目标时武器最终 alpha=0
        if (_transitionKind == TransitionKind.ToBlack)
            a = kindFromAlpha;
        else if (_transitionKind == TransitionKind.ToPink)
            a = kindToAlpha;
        else
            return;

        _weapon.Visible = a > 0.01f || _transitionKind == TransitionKind.ToPink;
        var m = _weapon.Modulate;
        m.A = Mathf.Clamp(a, 0f, 1f);
        _weapon.Modulate = m;
        // 与粉同 scale：from/to 缩放在 ApplyTransforms 用 _motionScale；此处用 form 阶段 scale
        // 武器 scale 直接跟粉当前 scale（from 或 to）
        if (_transitionKind == TransitionKind.ToBlack && _pink != null && GodotObject.IsInstanceValid(_pink))
            _weapon.Scale = _pink.Scale;
        else if (_transitionKind == TransitionKind.ToPink && _pink != null && GodotObject.IsInstanceValid(_pink))
            _weapon.Scale = _pink.Scale;
    }

    private void FinishFormTransition()
    {
        try
        {
            ResolveTransitionTargets(_transitionKind, out var from, out var to, out var mid, out var others);
            SetRectVisual(from, false, 1f, 1f, 0f);
            SetRectVisual(to, true, 1f, 1f, 0f);
            SetRectVisual(mid, false, 1f, 1f, 0f);
            foreach (var other in others)
                if (other != null && other != to)
                    SetRectVisual(other, false, 1f, 1f, 0f);

            if (_weapon != null && GodotObject.IsInstanceValid(_weapon) && !_weaponDeathActive)
            {
                if (_transitionKind == TransitionKind.ToBlack)
                {
                    _weapon.Visible = false;
                    _weapon.Modulate = Colors.White;
                    _weapon.Scale = Vector2.One;
                    _weapon.Rotation = 0f;
                }
                else if (_transitionKind == TransitionKind.ToPink)
                {
                    _weapon.Visible = true;
                    _weapon.Modulate = Colors.White;
                    _weapon.Scale = Vector2.One;
                    _weapon.Rotation = 0f;
                }
            }

            if (_transitionKind == TransitionKind.ToCorpse)
            {
                _state = AnimState.Dead;
                _animTime = 0;
                ResetMotion();
                // 武器落地后保持显示并置顶
                if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
                {
                    _weapon.Visible = true;
                    try
                    {
                        var parent = _weapon.GetParent();
                        if (parent != null)
                            parent.MoveChild(_weapon, parent.GetChildCount() - 1);
                    }
                    catch { }
                }
                if (_creatureNode != null && GodotObject.IsInstanceValid(_creatureNode))
                    DeniaFormPatch.SetFormTransitioning(_creatureNode, false);
                return;
            }
        }
        catch { }

        _state = AnimState.Idle;
        _animTime = 0;
        ResetMotion();
        _midExtraOffset = Vector2.Zero;
        _weaponExtraRot = 0f;
    }

    private void EnsureParentAllowsOverflow()
    {
        try
        {
            foreach (var rect in new[] { _midToBlack, _midToPink, _pink, _black, _corpse, _weapon, _blackFx })
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

    private void PlayFormSfxPhase1()
    {
        try { SfxCmd.Play(FormSfxPhase1); } catch { }
    }

    private void PlayFormSfxPhase2()
    {
        try
        {
            string sfx = "event:/sfx/characters/ironclad/ironclad_attack";
            try
            {
                var ch = _creatureNode?.Entity?.Player?.Character;
                if (ch is BaseLib.Abstracts.CustomCharacterModel custom
                    && !string.IsNullOrEmpty(custom.CustomAttackSfx))
                    sfx = custom.CustomAttackSfx;
                else if (ch != null && !string.IsNullOrEmpty(ch.AttackSfx))
                    sfx = ch.AttackSfx;
            }
            catch { }
            SfxCmd.Play(sfx);
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

        Vector2 midBlackPos = pos;
        Vector2 midPinkPos = pos;
        if (_transitionKind == TransitionKind.ToPink)
            midPinkPos = pos + _midExtraOffset;
        else if (_transitionKind == TransitionKind.ToBlack)
            midBlackPos = pos + _midExtraOffset;
        ApplyBody(_midToBlack, midBlackPos, centerPivot: true);
        ApplyBody(_midToPink, midPinkPos, centerPivot: true);
        ApplyBody(_corpse, pos, centerPivot: true);

        // 武器：中心在角色中心上方 30px；旋转绕武器自身中心；呼吸同竖直拉伸
        if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
        {
            _weapon.Size = _baseSize;
            bool deadWeapon = _weaponDeathActive
                || (_state == AnimState.Dead)
                || (_transitionKind == TransitionKind.ToCorpse && _corpse != null && _corpse.Visible);

            if (deadWeapon || _weaponDeathActive || (_state == AnimState.Dead && _weapon.Visible))
            {
                // 死亡脱手：绕左下角；落地后保持
                _weapon.PivotOffset = new Vector2(0f, _baseSize.Y);
                Vector2 weaponRestCenter = center + new Vector2(0f, WeaponCenterOffsetY);
                // 脱手起点：武器中心位置对应的左下角
                Vector2 bottomLeftRest = weaponRestCenter + new Vector2(-_baseSize.X * 0.5f, _baseSize.Y * 0.5f);
                Vector2 bottomLeftTarget = bottomLeftRest + _weaponDeathOffset;
                _weapon.Position = bottomLeftTarget - _weapon.PivotOffset;
                _weapon.Rotation = _weaponDeathRot;
                _weapon.Scale = Vector2.One;
                _weapon.Visible = true;
                try
                {
                    var parent = _weapon.GetParent();
                    if (parent != null)
                        parent.MoveChild(_weapon, parent.GetChildCount() - 1);
                }
                catch { }
            }
            else
            {
                // 常态/挥动：中心上移 30px，绕自身中心旋转
                _weapon.PivotOffset = _baseSize * 0.5f;
                Vector2 weaponPos = pos + new Vector2(0f, WeaponCenterOffsetY);
                // 竖直拉伸时保持中心：Scale.Y 变化绕中心 pivot 即可
                _weapon.Position = weaponPos;
                _weapon.Rotation = _weaponExtraRot; // 不叠加身体旋转（呼吸已无旋转）
                if (_state != AnimState.FormTransition)
                    _weapon.Scale = _motionScale;
            }
        }

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

            if (_state != AnimState.FormTransition)
                rect.Scale = _motionScale;

            Vector2 s = rect.Scale;
            // Control.Scale + PivotOffset 不可靠，用位置补偿钉锚点（PivotOffset=0）
            rect.PivotOffset = Vector2.Zero;
            if (_state == AnimState.FormTransition)
            {
                // 形态切换：绕几何中心缩放
                rect.Position = pos + new Vector2(
                    _baseSize.X * 0.5f * (1f - s.X),
                    _baseSize.Y * 0.5f * (1f - s.Y));
            }
            else
            {
                // 呼吸等：竖直钉底边，脚底不飘
                rect.Position = pos + new Vector2(
                    _baseSize.X * 0.5f * (1f - s.X),
                    _baseSize.Y * (1f - s.Y));
            }
        }
        catch { }
    }
}

/// <summary>
/// Death/revive triggers; pink attack swing via Attack; skill/power and black FX via BeforeCardPlayed.
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

            if (DeniaFormPatch.IsFormTransitioning(__instance) || DeniaFormPatch.IsDying(__instance))
                return;

            if (string.Equals(trigger, "Attack", StringComparison.OrdinalIgnoreCase)
                && !DeniaFormHelper.IsBlack(creature))
            {
                DeniaFormPatch.PlayWeaponSwing(creature, clockwise: true);
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
            if (node != null && (DeniaFormPatch.IsFormTransitioning(node) || DeniaFormPatch.IsDying(node)))
                return;

            bool isAttack = card.Type == CardType.Attack;
            DeniaFormPatch.BeginBlackFormCardPlay(creature, isAttack);

            bool isBlack = DeniaFormHelper.IsBlack(creature);
            if (isBlack)
            {
                if (isAttack)
                {
                    DeniaFormPatch.PlayBlackAttackFxSlash(creature);
                    await DeniaFormPatch.AwaitBlackFx(creature);
                }
                return;
            }

            if (card.Type == CardType.Skill || card.Type == CardType.Power)
            {
                DeniaFormPatch.PlayWeaponSwing(creature, clockwise: false);
                await DeniaFormPatch.AwaitWeaponSwing(creature);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class DeniaStartDeathAnimPatch
{
    public static void Postfix(NCreature __instance, bool shouldRemove, ref float __result)
    {
        try
        {
            if (__instance?.Entity?.Player?.Character is not Denia) return;
            DeniaFormPatch.PlayDeath(__instance.Entity);
            // 尸体变形 1.5s，武器脱手 0.5s 并行，取较长
            __result = DeniaFormPatch.FormTransitionDuration;
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
            __result = 1.0f; // +0.5 in AnimDie => 1.5s
        }
        catch { }
    }
}

