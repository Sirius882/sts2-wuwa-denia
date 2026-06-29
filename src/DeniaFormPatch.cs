using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

/// <summary>引爆事件——小熊玩偶/赝作矮星订阅此事件</summary>
public static class DeniaBurstEvents
{
    public static event Func<Creature, Creature, int, Task>? OnBurstDone;
    private static bool _isFiring;

    internal static async Task FireBurst(Creature target, Creature applier, int cap)
    {
        if (_isFiring) return;
        _isFiring = true;
        try
        {
            if (OnBurstDone != null)
            {
                try { await OnBurstDone.Invoke(target, applier, cap); }
                catch (Exception ex) { GD.PrintErr($"[Denia] FireBurst handler error: {ex.Message}"); }
            }
        }
        finally { _isFiring = false; }
    }
}
// ---- Patch 1: 双形态视觉 ----用自定义静态图覆盖形象
[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class DeniaFormPatch
{
    private static readonly Dictionary<NCreature, TextureRect> _pinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackOverlay = new();

    public static void Postfix(NCreature __instance)
    {
        DeniaRelicBurstHandler.Init();
        DeniaBuffTracker.Init();

        var creature = __instance.Entity;
        if (creature == null || !creature.IsPlayer) return;
        if (creature.Player?.Character is not Denia) return;

        if (!_pinkOverlay.ContainsKey(__instance))
        {
            try
            {
                var pinkTex = ResourceLoader.Load<Texture2D>(
                    "res://images/packed/character_select/denia_pink.png");
                var blackTex = ResourceLoader.Load<Texture2D>(
                    "res://images/packed/character_select/denia_black.png");

                var pink = MakeOverlay(pinkTex);
                var black = MakeOverlay(blackTex);

                __instance.AddChild(pink);
                __instance.AddChild(black);

                _pinkOverlay[__instance] = pink;
                _blackOverlay[__instance] = black;

                if (__instance.Visuals != null)
                    __instance.Visuals.Visible = false;
            }
            catch (Exception ex) { GD.PrintErr($"[Denia] Form overlay load error: {ex.Message}"); }
        }

        // 延迟设置位置（等 Bounds 算好）
        if (__instance.Visuals != null && GodotObject.IsInstanceValid(__instance.Visuals))
        {
            var bounds = GetBoundsNode(__instance.Visuals);
            if (bounds != null && GodotObject.IsInstanceValid(bounds))
                PositionOverlays(__instance, bounds);
        }

        RefreshForCreature(creature);
    }

    private static Control? GetBoundsNode(NCreatureVisuals visuals)
    {
        var sc = visuals.GetNodeOrNull<Node>("ScaleContainer");
        if (sc != null) return sc.GetNodeOrNull<Control>("Bounds");
        return visuals.GetNodeOrNull<Control>("Bounds");
    }

    private static void PositionOverlays(NCreature nc, Control bounds)
    {
        var offset = bounds.GlobalPosition - nc.GlobalPosition;
        var size = bounds.Size * nc.Visuals.Scale;

        if (_pinkOverlay.TryGetValue(nc, out var pink))
        {
            pink.Position = offset;
            pink.Size = size;
            pink.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            pink.StretchMode = TextureRect.StretchModeEnum.Scale;
        }
        if (_blackOverlay.TryGetValue(nc, out var black))
        {
            black.Position = offset;
            black.Size = size;
            black.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            black.StretchMode = TextureRect.StretchModeEnum.Scale;
        }
    }

    private static TextureRect MakeOverlay(Texture2D tex)
    {
        return new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
    }

    public static void RefreshForCreature(Creature creature)
    {
        var room = NCombatRoom.Instance;
        if (room == null) return;
        var node = room.GetCreatureNode(creature);
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        bool isBlack = DeniaFormHelper.GetForm(creature) == DeniaForm.Black;

        if (_pinkOverlay.TryGetValue(node, out var pink) && GodotObject.IsInstanceValid(pink))
            pink.Visible = !isBlack;
        if (_blackOverlay.TryGetValue(node, out var black) && GodotObject.IsInstanceValid(black))
            black.Visible = isBlack;
    }
}
// ---- Patch 2: 引爆事件钩子 — 通过后置条件（层数被清零）检测引爆 ----
/// Postfix 中 await 原始 Task 以保留返回值（防止多人 desync 篡改），
/// 同时 await FireBurst 确保引爆后补层数同步完成，避免 fire-and-forget 导致多人 desync。
/// 不使用 ConfigureAwait(false) 以避免线程池回调中触碰游戏状态。
[HarmonyPatch]
public static class DeniaBurstHook
{
    private static bool _burstInProgress;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathWw.Scripts.AemeathFusionBurstState), "TryAddFusionBurst",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(MegaCrit.Sts2.Core.Models.CardModel) });
    }

    public static void Postfix(ref Task<bool> __result, Creature target, int amount, Creature applier)
    {
        if (amount <= 0) return;
        if (_burstInProgress) return;
        __result = WrapBurstDetection(__result, target, amount, applier);
    }

    private static async Task<bool> WrapBurstDetection(
        Task<bool> originalTask, Creature target, int amount, Creature applier)
    {
        bool originalResult;
        try { originalResult = await originalTask; }
        catch { throw; }

        int after;
        try { after = AemeathWw.Scripts.AemeathFusionBurstState.GetFusionBurst(target); }
        catch { return originalResult; }

        if (after >= amount) return originalResult;

        _burstInProgress = true;
        try
        {
            var cap = AemeathWw.Scripts.AemeathFusionBurstState.GetFusionBurstCap(target);
            await DeniaBurstEvents.FireBurst(target, applier, cap);
        }
        finally { _burstInProgress = false; }

        return originalResult;
    }
}
// ---- Patch 2b: 虚质基础获取——粉色形态打出攻击牌 +2虚质 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class DeniaBaseVMGainPatch
{
    public static void Postfix(ref Task __result, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = cardPlay.Card.Owner;
        bool shouldGainVm = owner?.Character is Denia
            && cardPlay.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack
            && DeniaFormHelper.IsPink(owner.Creature);
        bool shouldTriggerTuneStrainResponse = cardPlay.Card.Keywords.Contains(DeniaSpecialKeywords.TuneStrainResponse);
        if (!shouldGainVm && !shouldTriggerTuneStrainResponse) return;

        __result = WrapAfterCardPlayed(__result, choiceContext, cardPlay, owner?.Creature, shouldGainVm, shouldTriggerTuneStrainResponse);
    }

    private static async Task WrapAfterCardPlayed(Task original, PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? creature, bool gainVm, bool triggerTuneStrainResponse)
    {
        await (original ?? Task.CompletedTask);
        if (gainVm && creature != null)
        {
            int vmAmount = 2;
            var candyPower = creature.GetPower<DeniaRainbowCandyJumpPower>();
            if (candyPower != null && candyPower.Amount > 0)
                vmAmount += 2 * candyPower.Amount;
            await DeniaResourceState.GainVirtualMatter(creature, vmAmount, creature, null!);
        }

        if (triggerTuneStrainResponse)
            await DeniaTuneStrainResponseEffect.AfterCardPlayed(choiceContext, cardPlay);
    }
}
// ---- Patch 3: 遗物效果注册 ----
public static class DeniaRelicBurstHandler
{
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        DeniaBurstEvents.OnBurstDone += OnBurst;
    }

    // 引爆后：持有骗术师/赝作矮星时，为目标附加其上限四分之一的聚爆；粉色形态额外获得2虚质（基础机制）
    private static async Task OnBurst(Creature target, Creature applier, int cap)
    {
        if (target.IsDead) return;
        if (applier?.IsPlayer != true) return;
        var player = applier.Player;
        if (player == null) return;

        // 引爆后补四分之一聚爆（需要遗物）
        bool hasTeddy = player.GetRelic<DeniaTrickster>() != null;
        bool hasDwarf = player.GetRelic<DeniaCounterfeitDwarfStar>() != null;
        if (hasTeddy || hasDwarf)
        {
            int add = cap / 4;
            if (add > 0)
                await AemeathWw.Scripts.AemeathFusionBurstState.TryAddFusionBurstWithoutAutoBurst(
                    target, add, applier, null!);
        }

        // 粉色形态触发引爆 → +2虚质（基础机制，不依赖遗物）
        if (DeniaFormHelper.IsPink(applier))
        {
            int vmAmount = 2;
            var candyPower = applier.GetPower<DeniaRainbowCandyJumpPower>();
            if (candyPower != null && candyPower.Amount > 0)
                vmAmount += 2 * candyPower.Amount;
            await DeniaResourceState.GainVirtualMatter(applier, vmAmount, applier, null!);
        }
    }
}
// ---- Patch 4: 相册粉色熔解保护——Patch Aemeath 内置的 ShouldPreserveFusionBurstOnMelt ----
[HarmonyPatch]
public static class DeniaMeltProtectPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathWw.Scripts.AemeathFusionBurstState), "ShouldPreserveFusionBurstOnMelt",
            new[] { typeof(Creature), typeof(MegaCrit.Sts2.Core.Models.CardModel) });
    }

    public static void Postfix(ref bool __result, Creature applier)
    {
        if (__result) return;
        if (applier?.IsPlayer != true) return;
        if (applier.Player?.GetRelic<DeniaAlbum>() == null) return;
        if (!DeniaFormHelper.IsPink(applier)) return;
        __result = true;
    }
}
// ---- Patch 5: 欧洛巴斯之触——骗术师 → 赝作矮星 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas), "GetUpgradedStarterRelic")]
public static class DeniaTouchOfOrobasPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Models.RelicModel starterRelic, ref MegaCrit.Sts2.Core.Models.RelicModel __result)
    {
        if (starterRelic is DeniaTrickster)
            __result = ModelDb.Relic<DeniaCounterfeitDwarfStar>().ToMutable();
    }
}
// ---- Patch 7: 回到远方——附加聚爆+1 ----
[HarmonyPatch]
public static class DeniaExtraBurstPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathWw.Scripts.AemeathFusionBurstState), "TryAddFusionBurst",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(MegaCrit.Sts2.Core.Models.CardModel) });
    }

    public static void Prefix(ref int amount, Creature applier)
    {
        if (DeniaMeltingAway.IsMeltingAwayBurstFill) return;
        if (applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaExtraBurstPower>();
        if (pwr != null) amount += (int)pwr.Amount;
    }
}
// ---- Patch 8: 从远方——附加聚爆上限+1 ----
[HarmonyPatch]
public static class DeniaExtraBurstCapPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathWw.Scripts.AemeathFusionBurstState), "TryIncreaseFusionBurstCap",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(MegaCrit.Sts2.Core.Models.CardModel) });
    }

    public static void Prefix(ref int amount, Creature applier)
    {
        if (applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaExtraBurstCapPower>();
        if (pwr != null) amount += (int)pwr.Amount;
    }
}
// ---- Patch 9: PowerCmd.ModifyAmount 钩子 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd), nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount))]
public static class DeniaModifyAmountPatch
{
    public static bool Prefix()
    {
        return true;
    }
}
// ---- Patch 9c: AfterCardPlayed flush（熵变强化格挡 + 匍炬松松子力量）----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class DeniaAccumulatorFlushPatch
{
    public static void Postfix(ref Task __result, CardPlay cardPlay)
    {
        var creature = cardPlay.Card.Owner?.Creature;
        if (creature == null) return;
        __result = WrapFlush(__result, creature);
    }

    private static async Task WrapFlush(Task original, Creature creature)
    {
        await original;
        await DeniaEntropyBoostPower.FlushBlockAsync(creature);
        await DeniaTorchPineNutPower.FlushStrengthAsync(creature);
    }
}
// ---- Patch 9d: 熵变强化——Hook BeforePowerAmountChanged（自己Buff + 敌人Debuff）----
/// 每次增益/减益均通过 BeforePowerAmountChanged 触发，按 instance 次数而非层数计算。
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforePowerAmountChanged))]
public static class DeniaEntropyPowerAppliedHook
{
    public static void Postfix(PowerModel power, decimal amount, Creature target, Creature? applier)
    {
        if (target == null) return;
        if (amount <= 0) return;
        // 自己获得增益 OR 给敌人附加减益
        bool isSelfBuff = target.IsPlayer && power.Type == PowerType.Buff;
        bool isEnemyDebuff = !target.IsPlayer && power.Type == PowerType.Debuff;
        if (!isSelfBuff && !isEnemyDebuff) return;
        // Buff时从 target取power；Debuff时从applier（施法的玩家）取power
        var owner = isSelfBuff ? target : applier;
        if (owner == null) return;
        var pwr = owner.GetPower<DeniaEntropyBoostPower>();
        if (pwr == null) return;
        int block = (int)pwr.Amount;
        DeniaEntropyBoostPower.AccumulateBlock(owner, block);
    }
}
// ---- Patch 10: 止痛药/压缩食品/相册能量/献斗/Solo轨迹/黯核基础——AfterSideTurnStart ----
/// 合并原来分散的两个 Patch（Patch 10 和 Patch 12），避免多个 Prefix(ref Task) 冲突。
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterSideTurnStart))]
public static class DeniaRelicTurnStartPatch
{
    private static bool _painkillerFirst;
    private static bool _swordFirst;
    internal static readonly HashSet<Creature> _hitThisCombat = new();
    private static readonly HashSet<Creature> _swordSetupDone = new();
    private static readonly Dictionary<Creature, bool> _wasSolo = new();

    static DeniaRelicTurnStartPatch()
    {
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatSetUp += _ =>
        {
            _painkillerFirst = false;
            _swordFirst = false;
            _hitThisCombat.Clear();
            _swordSetupDone.Clear();
            _wasSolo.Clear();
        };

        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatWon += room =>
        {
            if (room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss)
            {
                foreach (var player in room.CombatState.Players)
                {
                    var sword = player.GetRelic<DeniaMasterSword>();
                    if (sword != null)
                        sword.Counter = 40;
                }
            }
        };
    }

    public static void Prefix(ref Task __result, MegaCrit.Sts2.Core.Combat.ICombatState combatState, MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        // Player side: wrap to run after hooks complete
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            __result = WrapTurnStart(__result, combatState);
        }
        else if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Enemy)
        {
            // Enemy side: 楔丸 check (sync, no async wrapping needed)
            KusabimaruCheck(combatState);
        }
    }

    private static async Task WrapTurnStart(Task original, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        await (original ?? Task.CompletedTask);
        foreach (var player in combatState.Players)
        {
            // 相册：黑色形态 +1 能量
            if (player.GetRelic<DeniaAlbum>() != null && DeniaFormHelper.IsBlack(player.Creature))
                await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainEnergy(1m, player);

            // 止痛药：首回合 buff + 每回合易伤
            if (player.GetRelic<DeniaPainkiller>() != null)
            {
                if (!_painkillerFirst)
                {
                    _painkillerFirst = true;
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(new ThrowingPlayerChoiceContext(), player.Creature, 5m, player.Creature, null!);
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>(new ThrowingPlayerChoiceContext(), player.Creature, 5m, player.Creature, null!);
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(new ThrowingPlayerChoiceContext(), player.Creature, 50m, player.Creature, null!);
                }
                await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null!);
            }

            // 压缩食品：+6 活力
            if (player.GetRelic<DeniaRation>() != null)
                await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.VigorPower>(new ThrowingPlayerChoiceContext(), player.Creature, 6m, player.Creature, null!);

            bool notHit = !_hitThisCombat.Contains(player.Creature);

            // 献斗盾护符：首次掉血前每回合+6格挡
            if (notHit && player.GetRelic<DeniaSacrificialShield>() != null)
                await MegaCrit.Sts2.Core.Commands.CreatureCmd.GainBlock(
                    player.Creature, new MegaCrit.Sts2.Core.Localization.DynamicVars.BlockVar(6m, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move), null);

            // 献斗剑护符：战斗开始时+30聚爆轨迹+6力量（首次掉血后移除效果）
            if (player.GetRelic<DeniaSacrificialSword>() != null)
            {
                if (!_swordFirst)
                {
                    _swordFirst = true;
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(new ThrowingPlayerChoiceContext(), player.Creature, 30m, player.Creature, null!);
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(new ThrowingPlayerChoiceContext(), player.Creature, 6m, player.Creature, null!);
                }
            }

            // --- 角色基础逻辑：粉色形态每回合+1黯核，生日蛋糕每层额外+1 ---
            if (player.Character is Denia && DeniaFormHelper.IsPink(player.Creature)
                && DeniaResourceState.GetDarkCore(player.Creature) < DeniaResourceState.DarkCoreMax)
            {
                int dcGain = 1;
                var cake = player.Creature.GetPower<DeniaBirthdayCakePower>();
                if (cake != null) dcGain += (int)cake.Amount;
                await DeniaResourceState.GainDarkCore(player.Creature, dcGain, player.Creature, null!);
            }

            // --- 大师之剑：战斗开始时若计数>0给2力量 ---
            var sword = player.GetRelic<DeniaMasterSword>();
            if (sword != null && !_swordSetupDone.Contains(player.Creature))
            {
                _swordSetupDone.Add(player.Creature);
                bool isBoss = player.RunState.CurrentRoom.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss;
                if ((!isBoss && sword.Counter > 0) || isBoss)
                {
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                        new ThrowingPlayerChoiceContext(), player.Creature, 2m, player.Creature, null!);
                    sword.GrantedStrength = 2m;
                }
            }

            // --- 大师之剑：Boss胜利后计数恢复40 ---
            // handled in OnCombatWon subscription via DeniaMasterSword.AfterObtained/AfterRoomEntered

            // 骗术师/赝作矮星：敌方仅有一名目标时，维持一份30聚爆轨迹
            await RefreshSoloTrajectory(player, combatState);
        }

        // 熵变强化/匍炬松松子累加器 flush（安全发放）
        foreach (var player in combatState.Players)
        {
            await DeniaEntropyBoostPower.FlushBlockAsync(player.Creature);
            await DeniaTorchPineNutPower.FlushStrengthAsync(player.Creature);
        }
    }

    private static async Task RefreshSoloTrajectory(MegaCrit.Sts2.Core.Entities.Players.Player player, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        bool hasTeddy = player.GetRelic<DeniaTrickster>() != null;
        bool hasDwarf = player.GetRelic<DeniaCounterfeitDwarfStar>() != null;
        if (!hasTeddy && !hasDwarf) return;

        bool solo = combatState.Enemies.Count(e => !e.IsDead) == 1;
        bool was = _wasSolo.TryGetValue(player.Creature, out bool w) && w;
        if (solo == was) return;

        _wasSolo[player.Creature] = solo;
        if (!solo)
        {
            var traj = player.Creature.GetPower<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>();
            if (traj != null && traj.Amount >= 30m)
                await MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(), traj, -30m, player.Creature, null!);
            return;
        }

        await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(
            new ThrowingPlayerChoiceContext(), player.Creature, 30m, player.Creature, null!);
    }

    private static void KusabimaruCheck(MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        bool hasRelic = combatState.Players.Any(p =>
            p.GetRelic<DeniaKusabimaru>() != null);
        if (!hasRelic) return;

        foreach (var enemy in combatState.Enemies)
        {
            if (enemy.IsDead) continue;
            var attackIntents = enemy.Monster?.NextMove?.Intents
                ?.OfType<MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent>();
            if (attackIntents == null || !attackIntents.Any()) continue;

            int intentDamage = attackIntents.Sum(i =>
                i.GetTotalDamage(combatState.Enemies, enemy));
            if (intentDamage <= 0) continue;

            int taken = DeniaKusabimaru.TurnDamage.GetValueOrDefault(enemy, 0);
            if (taken == intentDamage)
                _ = MegaCrit.Sts2.Core.Commands.CreatureCmd.Stun(enemy);
        }
    }
}

// ---- Patch 12: 删除（合并到 Patch 10 中）----
// ---- Patch 14: 献斗遗物HP追踪——AfterCurrentHpChanged ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCurrentHpChanged))]
public static class DeniaSacrificeHpTrackPatch
{
    private static void Postfix(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, decimal delta)
    {
        if (delta < 0 && creature.IsPlayer)
            DeniaRelicTurnStartPatch._hitThisCombat.Add(creature);
    }
}
// ---- Patch 20: 卡面填充拉伸 ---- 覆盖 NCard._Ready 后设置 Portrait 填充父容器
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), "_Ready")]
public static class DeniaCardPortraitFillPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(__instance)) return;
            var portrait = __instance.GetNodeOrNull<TextureRect>("%Portrait");
            if (portrait != null && GodotObject.IsInstanceValid(portrait))
            {
                portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                portrait.StretchMode = TextureRect.StretchModeEnum.Scale;
            }
        }
        catch { }
    }
}
// ---- Patch 15: 匍炬松松子——获得聚爆轨迹时获得1/5力量 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforePowerAmountChanged))]
public static class DeniaTorchPineNutPatch
{
    public static void Postfix(PowerModel power, decimal amount, Creature target)
    {
        if (amount <= 0) return;
        if (target?.IsPlayer != true) return;
        if (power is not AemeathFusionBurstTrajectoryPower) return;

        var pwr = target.GetPower<DeniaTorchPineNutPower>();
        if (pwr == null) return;

        int strGain = (int)amount / 5 * (int)pwr.Amount;
        if (strGain > 0)
            DeniaTorchPineNutPower.AccumulateStrength(target, strGain);
    }
}
// ---- Patch 19: 聚爆自动引爆安全补丁 ----
/// Aemeath TryTriggerAutoBurst 使用 BlockingPlayerChoiceContext 造成伤害，
/// 该上下文在玩家回合内可能触发死锁（新版本 action 系统更严格）。
/// 替换为 ThrowingPlayerChoiceContext：自动引爆伤害为 Unpowered 属性，不会触发玩家选择。
[HarmonyPatch]
public static class DeniaSafeAutoBurstPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathWw.Scripts.AemeathFusionBurstState), "TryTriggerAutoBurst",
            new[] { typeof(Creature), typeof(Creature), typeof(MegaCrit.Sts2.Core.Models.CardModel) });
    }

    /// <summary>跳过原始方法，用安全版本替代</summary>
    public static bool Prefix(Creature target, Creature? applier, CardModel? source, ref Task<bool> __result)
    {
        __result = SafeTriggerAutoBurst(target, applier, source);
        return false;
    }

    private static async Task<bool> SafeTriggerAutoBurst(Creature target, Creature? applier, CardModel? source)
    {
        if (target.IsDead) return false;
        if (!AemeathWw.Scripts.AemeathFusionBurstState.IsAtFusionBurstCap(target)) return false;
        if (AemeathWw.Scripts.AemeathFusionBurstState.HasAutoBurstSuppressed(target)) return false;
        if (AemeathWw.Scripts.AemeathFusionBurstState.IsAutoBurstDisabledForCombat(target)) return false;

        // 设置 IsBurstProcessing 标志（通过反射访问私有 setter）
        var isBurstProcessingProp = typeof(AemeathWw.Scripts.AemeathFusionBurstState)
            .GetProperty("IsBurstProcessing", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        bool prevBurst = false;
        try
        {
            if (isBurstProcessingProp != null)
            {
                prevBurst = (bool)(isBurstProcessingProp.GetValue(null) ?? false);
                isBurstProcessingProp.SetValue(null, true);
            }

            int burstCap = AemeathWw.Scripts.AemeathFusionBurstState.GetFusionBurstCap(target);
            int rawDamage = AemeathWw.Scripts.AemeathFusionBurstState.GetBurstDamage(burstCap);
            // ApplyFusionBurstDamageBonus 是私有方法，无法直接调用；
            // 使用原始方法中的计算逻辑：轨迹每层 +1%
            var traj = applier?.GetPower<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>();
            int trajStacks = traj?.Amount ?? 0;
            int damage = trajStacks > 0 ? (int)(rawDamage * (1m + trajStacks / 100m)) : rawDamage;

            var enemies = target.CombatState?.HittableEnemies?.Where(e => !e.IsDead).ToArray()
                ?? Array.Empty<Creature>();

            // 先清除聚爆
            await AemeathWw.Scripts.AemeathFusionBurstState.ClearFusionBurst(target);

            if (damage > 0 && enemies.Length > 0)
            {
                foreach (var enemy in enemies)
                {
                    // 使用 ThrowingPlayerChoiceContext 替代 BlockingPlayerChoiceContext
                    await MegaCrit.Sts2.Core.Commands.CreatureCmd.Damage(
                        new ThrowingPlayerChoiceContext(), enemy, (decimal)damage,
                        MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, applier, source);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] SafeAutoBurst error: {ex.Message}");
            return false;
        }
        finally
        {
            if (isBurstProcessingProp != null)
                isBurstProcessingProp.SetValue(null, prevBurst);
        }
    }
}
// ---- Patch 17: 虚质科学直觉——每消耗10虚质获得1能量 ----
/// 累加虚质消耗量，通过 AfterCardPlayed 安全发放能量。
[HarmonyPatch(typeof(DeniaResourceState), nameof(DeniaResourceState.TrySpendVirtualMatter))]
public static class DeniaVMIntuitionPatch
{
    private static void Postfix(Task<bool> __result, Creature creature, int amount)
    {
        if (amount <= 0) return;
        _ = __result.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result)
                DeniaVirtualScienceIntuitionPower.AccumulateVM(creature, amount);
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion);
    }
}
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class DeniaVMIntuitionFlushPatch
{
    public static void Postfix(ref Task __result, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null) return;
        __result = WrapVMFlush(__result, player);
    }

    private static async Task WrapVMFlush(Task original, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        await original;
        await DeniaVirtualScienceIntuitionPower.FlushEnergyAsync(player);
    }
}
// ---- Patch 18: 尘封魔典补丁——兜底调用 SetupForPlayer（控制台发放等场景未调用）----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Relics.DustyTome), nameof(MegaCrit.Sts2.Core.Models.Relics.DustyTome.AfterObtained))]
public static class DeniaDustyTomePatch
{
    private static readonly System.Reflection.FieldInfo? _ancientCardField =
        AccessTools.Field(typeof(MegaCrit.Sts2.Core.Models.Relics.DustyTome), "_ancientCard");

    private static bool Prefix(MegaCrit.Sts2.Core.Models.Relics.DustyTome __instance)
    {
        if (_ancientCardField == null) return true;
        if (_ancientCardField.GetValue(__instance) != null) return true;

        // AncientCard 为空，兜底调用 SetupForPlayer
        try { __instance.SetupForPlayer(__instance.Owner); }
        catch { return false; }
        return true;
    }
}

// ---- Patch 19: 大师之剑攻击牌计数 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class DeniaMasterSwordPatch
{
    public static void Prefix(CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null) return;
        if (cardPlay.Card.Type != MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack) return;

        var sword = player.GetRelic<DeniaMasterSword>();
        if (sword == null) return;

        bool isBoss = player.RunState.CurrentRoom.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss;
        if (isBoss) return;

        if (sword.Counter > 0)
        {
            sword.Counter--;
            sword.RefreshDisplay();
            if (sword.Counter == 0 && sword.GrantedStrength > 0)
            {
                var str = player.Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
                if (str != null && str.Amount >= sword.GrantedStrength)
                    _ = MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(
                        new ThrowingPlayerChoiceContext(), str, -sword.GrantedStrength, player.Creature, null!);
                sword.GrantedStrength = 0;
            }
        }
    }
    // Also handle 继续逃啊？/ 你也试试？
    public static void Postfix(CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null) return;
        DeniaKeepRunningPower.OnAnyCardPlayed(player, cardPlay);
        DeniaYouTryItPower.OnAnyCardPlayed(player, cardPlay);
    }
}

// ---- Patch 20: 楔丸——追踪回合伤害 ----
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterDamageReceived))]
public static class DeniaKusabimaruDamagePatch
{
    static DeniaKusabimaruDamagePatch()
    {
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.TurnStarted += _ =>
            DeniaKusabimaru.TurnDamage.Clear();
    }

    public static void Postfix(
        MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target)
    {
        if (!target.IsMonster) return;
        if (result.UnblockedDamage <= 0) return;

        if (!DeniaKusabimaru.TurnDamage.ContainsKey(target))
            DeniaKusabimaru.TurnDamage[target] = 0;
        DeniaKusabimaru.TurnDamage[target] += result.UnblockedDamage;
    }
}

// ---- Patch 21: 删除（合并到 Patch 10 中）----

// ---- Patch 22: 删除（旧集谐系统逻辑已迁移）----
// ---- Patch 23: 达妮娅能量图标 → 纯文字 "能量" ----
/// EnergyIconsFormatter 通过 [img] BBCode 嵌入能量图标，但 Godot RichTextLabel 不缩放 [img]。
/// 达妮娅的能量图标过大，会撑破文字行。改为写入纯文字 "能量" / "2能量" 等。
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Localization.Formatters.EnergyIconsFormatter), nameof(MegaCrit.Sts2.Core.Localization.Formatters.EnergyIconsFormatter.TryEvaluateFormat))]
public static class DeniaEnergyIconTextPatch
{
    public static bool Prefix(SmartFormat.Core.Extensions.IFormattingInfo formattingInfo, ref bool __result)
    {
        // 提取 prefix
        string? prefix = null;
        if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.EnergyVar ev)
            prefix = ev.ColorPrefix;

        if (string.IsNullOrEmpty(prefix))
            prefix = formattingInfo.CurrentValue as string;

        if (string.IsNullOrEmpty(prefix) || prefix == "colorless")
            prefix = MegaCrit.Sts2.Core.Runs.RunManager.Instance.GetLocalCharacterEnergyIconPrefix();

        // 非达妮娅 → 走原逻辑
        if (prefix != "denia")
            return true;

        // 提取数量
        int count = 1;
        if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.EnergyVar ev2)
            count = Convert.ToInt32(ev2.PreviewValue);
        else if (formattingInfo.CurrentValue is MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar cv)
            count = Convert.ToInt32(cv.Calculate(null));
        else if (formattingInfo.CurrentValue is int i)
            count = i;
        else if (formattingInfo.CurrentValue is decimal d)
            count = (int)d;
        else if (formattingInfo.CurrentValue is string s && int.TryParse(formattingInfo.FormatterOptions, out int parsed))
            count = parsed;

        // 输出纯文字
        string text = count switch { 1 => "能量", 2 => "2能量", 3 => "3能量", _ => $"{count}能量" };
        formattingInfo.Write(text);
        __result = true;
        return false;
    }
}