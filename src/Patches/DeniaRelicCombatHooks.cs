using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace Denia;

/// <summary>欧洛巴斯之触：骗术师 → 赝作矮星</summary>
[HarmonyPatch(typeof(TouchOfOrobas), "GetUpgradedStarterRelic")]
public static class DeniaTouchOfOrobasPatch
{
    public static void Postfix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is DeniaTrickster)
            __result = ModelDb.Relic<DeniaCounterfeitDwarfStar>().ToMutable();
    }
}

/// <summary>回合开始：遗物效果 + 黯核 + flush；敌方：楔丸眩晕。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterSideTurnStart))]
public static class DeniaRelicTurnStartPatch
{
    static DeniaRelicTurnStartPatch()
    {
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatSetUp += combatState =>
        {
            foreach (var player in combatState.Players)
            {
                var sacrificialSword = player.GetRelic<DeniaSacrificialSword>();
                if (sacrificialSword == null) continue;
                sacrificialSword.GrantedStrength = 0m;
                sacrificialSword.GrantedShroudedStar = 0m;
                sacrificialSword.EffectRemoved = false;
            }
        };

        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatWon += room =>
        {
            if (room.RoomType != RoomType.Boss) return;
            foreach (var player in room.CombatState.Players)
            {
                var sword = player.GetRelic<DeniaMasterSword>();
                if (sword != null)
                    sword.Counter = 40;
            }
        };
    }

    public static void Postfix(
        ref Task __result,
        MegaCrit.Sts2.Core.Combat.ICombatState combatState,
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player)
            __result = WrapTurnStart(__result, combatState);
        else if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Enemy)
            __result = WrapEnemyTurnStart(__result, combatState);
    }

    private static async Task WrapEnemyTurnStart(Task original, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        await (original ?? Task.CompletedTask);
        await KusabimaruCheckAsync(combatState);
    }

    private static async Task WrapTurnStart(Task original, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        await DeniaBuffTracker.ClearTurnMarkers(combatState);
        await (original ?? Task.CompletedTask);
        foreach (var player in combatState.Players)
        {
            if (player.GetRelic<DeniaAlbum>() != null && DeniaFormHelper.IsBlack(player.Creature))
                await PlayerCmd.GainEnergy(1m, player);

            // 止痛药效果改由 DeniaPainkiller.AfterSideTurnStart 仅对持有者结算

            if (player.GetRelic<DeniaRation>() != null)
                await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), player.Creature, 6m, player.Creature, null!);

            bool notHit = player.Creature.GetPower<DeniaSacrificialHitThisCombatPower>() == null;

            if (notHit && player.GetRelic<DeniaSacrificialShield>() != null)
                await CreatureCmd.GainBlock(
                    player.Creature,
                    new MegaCrit.Sts2.Core.Localization.DynamicVars.BlockVar(6m, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move),
                    null);

            var sacrificialSword = player.GetRelic<DeniaSacrificialSword>();
            if (sacrificialSword != null)
            {
                if (!sacrificialSword.EffectRemoved && sacrificialSword.GrantedStrength <= 0m && sacrificialSword.GrantedShroudedStar <= 0m)
                {
                    const decimal swordStar = 2m;
                    const decimal swordStrength = 2m;
                    await PowerCmd.Apply<DeniaShroudedStarPower>(new ThrowingPlayerChoiceContext(), player.Creature, swordStar, player.Creature, null!);
                    await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), player.Creature, swordStrength, player.Creature, null!);
                    sacrificialSword.GrantedShroudedStar = swordStar;
                    sacrificialSword.GrantedStrength = swordStrength;
                }
            }

            if (player.Character is Denia && DeniaFormHelper.IsPink(player.Creature)
                && DeniaResourceState.GetDarkCore(player.Creature) < DeniaResourceState.DarkCoreMax)
            {
                int dcGain = 1;
                var cake = player.Creature.GetPower<DeniaBirthdayCakePower>();
                if (cake != null) dcGain += (int)cake.Amount;
                await DeniaResourceState.GainDarkCore(player.Creature, dcGain, player.Creature, null!);
            }

            var sword = player.GetRelic<DeniaMasterSword>();
            if (sword != null && player.Creature.GetPower<DeniaMasterSwordSetupDonePower>() == null)
            {
                await PowerCmd.Apply<DeniaMasterSwordSetupDonePower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null!);
                bool isBoss = player.RunState.CurrentRoom.RoomType == RoomType.Boss;
                if ((!isBoss && sword.Counter > 0) || isBoss)
                {
                    await PowerCmd.Apply<StrengthPower>(
                        new ThrowingPlayerChoiceContext(), player.Creature, 2m, player.Creature, null!);
                    await PowerCmd.Apply<DeniaShroudedStarPower>(
                        new ThrowingPlayerChoiceContext(), player.Creature, 2m, player.Creature, null!);
                    sword.GrantedStrength = 2m;
                    sword.GrantedShroudedStar = 2m;
                }
            }
        }

        foreach (var player in combatState.Players)
        {
            await PowerCmd.Remove<DeniaPhantomFoamTriggeredThisTurnPower>(player.Creature);
            await PowerCmd.Remove<DeniaTowardVoidTriggeredThisTurnPower>(player.Creature);
            await DeniaEntropyBoostPower.ClearTriggerCountAsync(player.Creature);
        }
    }

    private static async Task KusabimaruCheckAsync(MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        bool hasRelic = combatState.Players.Any(p => p.GetRelic<DeniaKusabimaru>() != null);
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
            // 差值绝对值 ≤ 2
            if (taken > 0 && System.Math.Abs(taken - intentDamage) <= 2)
                await CreatureCmd.Stun(enemy);
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCurrentHpChanged))]
public static class DeniaSacrificeHpTrackPatch
{
    public static void Postfix(ref Task __result, Creature creature, decimal delta)
    {
        if (delta < 0 && creature.IsPlayer)
            __result = WrapHpLoss(__result, creature);
    }

    private static async Task WrapHpLoss(Task original, Creature creature)
    {
        await (original ?? Task.CompletedTask);
        if (creature.GetPower<DeniaSacrificialHitThisCombatPower>() == null)
            await PowerCmd.Apply<DeniaSacrificialHitThisCombatPower>(
                new ThrowingPlayerChoiceContext(), creature, 1m, creature, null!);

        var player = creature.Player;
        if (player == null) return;
        var sword = player.GetRelic<DeniaSacrificialSword>();
        if (sword == null || sword.EffectRemoved) return;
        sword.EffectRemoved = true;

        if (sword.GrantedStrength > 0m)
        {
            var str = creature.GetPower<StrengthPower>();
            if (str != null && str.Amount > 0m)
            {
                decimal strToRemove = System.Math.Min(str.Amount, sword.GrantedStrength);
                await PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(), str, -strToRemove, creature, null!);
            }
            sword.GrantedStrength = 0m;
        }

        if (sword.GrantedShroudedStar > 0m)
        {
            var star = creature.GetPower<DeniaShroudedStarPower>();
            if (star != null && star.Amount > 0m)
            {
                decimal starToRemove = System.Math.Min(star.Amount, sword.GrantedShroudedStar);
                await PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(), star, -starToRemove, creature, null!);
            }
            sword.GrantedShroudedStar = 0m;
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterDamageReceived))]
public static class DeniaKusabimaruDamagePatch
{
    static DeniaKusabimaruDamagePatch()
    {
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.TurnStarted += _ =>
            DeniaKusabimaru.TurnDamage.Clear();
    }

    public static void Postfix(DamageResult result, Creature target)
    {
        if (!target.IsMonster) return;
        if (result.UnblockedDamage <= 0) return;

        if (!DeniaKusabimaru.TurnDamage.ContainsKey(target))
            DeniaKusabimaru.TurnDamage[target] = 0;
        DeniaKusabimaru.TurnDamage[target] += result.UnblockedDamage;
    }
}

[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.AfterObtained))]
public static class DeniaDustyTomePatch
{
    private static readonly System.Reflection.FieldInfo? _ancientCardField =
        AccessTools.Field(typeof(DustyTome), "_ancientCard");

    private static bool Prefix(DustyTome __instance)
    {
        if (_ancientCardField == null) return true;
        if (_ancientCardField.GetValue(__instance) != null) return true;
        try { __instance.SetupForPlayer(__instance.Owner); }
        catch { return false; }
        return true;
    }
}
