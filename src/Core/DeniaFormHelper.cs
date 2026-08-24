using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

public static class DeniaFormHelper
{
    private static readonly PlayerChoiceContext _throwing = new ThrowingPlayerChoiceContext();

    public static DeniaForm GetForm(Creature creature)
    {
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null || power.Amount <= 0) return DeniaForm.Pink;
        return DeniaForm.Black;
    }

    public static bool IsPink(Creature creature) => GetForm(creature) == DeniaForm.Pink;
    public static bool IsBlack(Creature creature) => GetForm(creature) == DeniaForm.Black;

    public static bool HasSwitchedFormThisTurn(Creature creature) =>
        creature.GetPower<DeniaFormSwitchedThisTurnPower>()?.Amount > 0;

    public static bool HasBlackFormStrengthChoice(Creature creature) =>
        creature.GetPower<DeniaBlackFormTemporaryResonanceModePower>()?.Amount > 0;

    public static bool HasBlackFormTrajectoryChoice(Creature creature) =>
        creature.GetPower<DeniaBlackFormTrajectoryDebtPower>()?.Amount > 0;

    public static bool SawLookAtMeThisBlackForm(Creature creature) =>
        creature.GetPower<DeniaBlackFormLookAtMeSeenPower>()?.Amount > 0;

    public static bool SawPityMeThisBlackForm(Creature creature) =>
        creature.GetPower<DeniaBlackFormPityMeSeenPower>()?.Amount > 0;

    public static bool SawForgiveMePathThisBlackForm(Creature creature) =>
        creature.GetPower<DeniaBlackFormForgiveMePathPower>()?.Amount > 0;

    public static async Task MarkLookAtMeSeenThisBlackForm(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaBlackFormLookAtMeSeenPower>() == null)
            await PowerCmd.Apply<DeniaBlackFormLookAtMeSeenPower>(_throwing, creature, 1m, applier, source);
    }

    public static async Task MarkPityMeSeenThisBlackForm(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaBlackFormPityMeSeenPower>() == null)
            await PowerCmd.Apply<DeniaBlackFormPityMeSeenPower>(_throwing, creature, 1m, applier, source);
    }

    public static async Task MarkForgiveMePathThisBlackForm(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaBlackFormForgiveMePathPower>() == null)
            await PowerCmd.Apply<DeniaBlackFormForgiveMePathPower>(_throwing, creature, 1m, applier, source);
    }

    public static async Task MarkResonanceModePermanent(Creature creature)
    {
        if (creature.GetPower<DeniaPermanentResonanceModeSeenPower>() == null)
            await PowerCmd.Apply<DeniaPermanentResonanceModeSeenPower>(_throwing, creature, 1m, creature, null!);
    }

    public static async Task AddBlackFormStrengthDebt(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount > 0)
            await PowerCmd.Apply<DeniaBlackFormStrengthDebtPower>(_throwing, creature, amount, applier, source);
    }

    public static async Task AddBlackFormTrajectoryDebt(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount > 0)
            await PowerCmd.Apply<DeniaBlackFormTrajectoryDebtPower>(_throwing, creature, amount, applier, source);
    }

    public static async Task AddWeaknessBonusStrengthDebt(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount > 0)
            await PowerCmd.Apply<DeniaWeaknessBonusStrengthDebtPower>(_throwing, creature, amount, applier, source);
    }

    public static async Task AddWeaknessBonusTrajectoryDebt(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount > 0)
            await PowerCmd.Apply<DeniaWeaknessBonusTrajectoryDebtPower>(_throwing, creature, amount, applier, source);
    }

    public static async Task AddBlackFormShroudedStarDebt(Creature creature, int amount, Creature applier, CardModel source)
    {
        if (amount > 0)
            await PowerCmd.Apply<DeniaBlackFormShroudedStarDebtPower>(_throwing, creature, amount, applier, source);
    }

    public static async Task MarkTemporaryResonanceMode(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaPermanentResonanceModeSeenPower>() != null) return;
        if (creature.GetPower<DeniaBlackFormTemporaryResonanceModePower>() == null)
            await PowerCmd.Apply<DeniaBlackFormTemporaryResonanceModePower>(_throwing, creature, 1m, applier, source);
    }

    public static async Task SwitchToBlack(Creature creature, Creature applier, CardModel source, bool addBlackFormCards = true)
    {
        bool wasPink = IsPink(creature);
        // 视觉变形先启动，再 await 播完（Fast：动画×2 → 等 0.75s，仍阻塞逻辑）
        if (wasPink)
            DeniaFormPatch.PlayFormTransition(creature, toBlack: true);
        await MarkFormSwitchedThisTurn(creature, applier, source);
        if (wasPink)
            await Cmd.Wait(DeniaFormPatch.GetFormTransitionWaitDuration());
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null)
            await PowerCmd.Apply<DeniaFormPower>(_throwing, creature, 1m, applier, source);
        else if (power.Amount <= 0)
            await PowerCmd.ModifyAmount(_throwing, power, 1m, applier, source);
        DeniaCardFrameMaterialPatch.RefreshForForm(creature);
        if (wasPink && addBlackFormCards && creature.Player != null)
            await AddBlackFormCards(creature.Player);
        DeniaFormPatch.EndFormTransition(creature);
        await DeniaResourceState.GainVirtualMatter(creature, 10, applier, source);
        await ApplyFormSwitchEffects(creature, applier, source);
    }

    private static async Task AddBlackFormCards(Player owner)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState == null) return;
        await CardPileCmd.AddGeneratedCardToCombat(
            combatState.CreateCard<DeniaLookAtMe>(owner), PileType.Hand, owner);
        await CardPileCmd.AddGeneratedCardToCombat(
            combatState.CreateCard<DeniaPityMe>(owner), PileType.Hand, owner);
    }

    public static async Task SwitchToPink(Creature creature, Creature applier, CardModel source, bool clearVM = true)
    {
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null || power.Amount <= 0) return;

        // 已在黑色形态：先播变形视觉并等待播完，再改 power
        DeniaFormPatch.PlayFormTransition(creature, toBlack: false);
        await MarkFormSwitchedThisTurn(creature, applier, source);
        await Cmd.Wait(DeniaFormPatch.GetFormTransitionWaitDuration());

        await PowerCmd.ModifyAmount(_throwing, power, -1m, applier, source);
        DeniaCardFrameMaterialPatch.RefreshForForm(creature);

        if (creature.GetPower<DeniaBlackFormTemporaryResonanceModePower>() != null
            && creature.GetPower<DeniaPermanentResonanceModeSeenPower>() == null)
        {
            await PowerCmd.Remove<DeniaResonanceModePower>(creature);
        }
        await PowerCmd.Remove<DeniaBlackFormTemporaryResonanceModePower>(creature);

        var strengthDebt = creature.GetPower<DeniaBlackFormStrengthDebtPower>();
        if (strengthDebt != null && strengthDebt.Amount > 0)
        {
            var str = creature.GetPower<StrengthPower>();
            int strToRemove = (int)strengthDebt.Amount;
            if (str != null && str.Amount > 0)
                await PowerCmd.ModifyAmount(_throwing, str, -Math.Min(strToRemove, (int)str.Amount), applier, source);
        }
        await PowerCmd.Remove<DeniaBlackFormStrengthDebtPower>(creature);

        var trajectoryDebt = creature.GetPower<DeniaBlackFormTrajectoryDebtPower>();
        if (trajectoryDebt != null && trajectoryDebt.Amount > 0)
        {
            var traj = creature.GetPower<AemeathFusionBurstTrajectoryPower>();
            int trajToRemove = (int)trajectoryDebt.Amount;
            if (traj != null && traj.Amount > 0)
                await PowerCmd.ModifyAmount(_throwing, traj, -Math.Min(trajToRemove, (int)traj.Amount), applier, source);
        }
        await PowerCmd.Remove<DeniaBlackFormTrajectoryDebtPower>(creature);

        var shroudedStarDebt = creature.GetPower<DeniaBlackFormShroudedStarDebtPower>();
        if (shroudedStarDebt != null && shroudedStarDebt.Amount > 0)
        {
            var star = creature.GetPower<DeniaShroudedStarPower>();
            int starToRemove = (int)shroudedStarDebt.Amount;
            if (star != null && star.Amount > 0)
                await PowerCmd.ModifyAmount(_throwing, star, -Math.Min(starToRemove, (int)star.Amount), applier, source);
        }
        await PowerCmd.Remove<DeniaBlackFormShroudedStarDebtPower>(creature);
        await PowerCmd.Remove<DeniaBlackFormLookAtMeSeenPower>(creature);
        await PowerCmd.Remove<DeniaBlackFormPityMeSeenPower>(creature);
        await PowerCmd.Remove<DeniaBlackFormForgiveMePathPower>(creature);

        var weaknessStrengthDebt = creature.GetPower<DeniaWeaknessBonusStrengthDebtPower>();
        if (weaknessStrengthDebt != null && weaknessStrengthDebt.Amount > 0)
        {
            var str = creature.GetPower<StrengthPower>();
            int strToRemove = (int)weaknessStrengthDebt.Amount;
            if (str != null && str.Amount > 0)
                await PowerCmd.ModifyAmount(_throwing, str, -Math.Min(strToRemove, (int)str.Amount), applier, source);
        }
        await PowerCmd.Remove<DeniaWeaknessBonusStrengthDebtPower>(creature);

        var weaknessTrajectoryDebt = creature.GetPower<DeniaWeaknessBonusTrajectoryDebtPower>();
        if (weaknessTrajectoryDebt != null && weaknessTrajectoryDebt.Amount > 0)
        {
            var star = creature.GetPower<DeniaShroudedStarPower>();
            int starToRemove = (int)weaknessTrajectoryDebt.Amount;
            if (star != null && star.Amount > 0)
                await PowerCmd.ModifyAmount(_throwing, star, -Math.Min(starToRemove, (int)star.Amount), applier, source);
        }
        await PowerCmd.Remove<DeniaWeaknessBonusTrajectoryDebtPower>(creature);

        if (clearVM)
        {
            // 切换前拥有7虚质 → 抽1张牌
            if (DeniaResourceState.GetVirtualMatter(creature) >= 7)
                await CardPileCmd.Draw(_throwing, 1, creature.Player);
            await DeniaResourceState.ClearVirtualMatter(creature, applier, source);
        }
        DeniaFormPatch.EndFormTransition(creature);

        await ApplyFormSwitchEffects(creature, applier, source);
    }

    private static async Task MarkFormSwitchedThisTurn(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaFormSwitchedThisTurnPower>() == null)
            await PowerCmd.Apply<DeniaFormSwitchedThisTurnPower>(_throwing, creature, 1m, applier, source);
    }

    /// <summary>形态切换后分发相关能力效果（夏耶/卡纽/阿马罗）。</summary>
    private static async Task ApplyFormSwitchEffects(Creature creature, Creature applier, CardModel source)
    {
        var xiaYe = creature.GetPower<DeniaXiaYePower>();
        if (xiaYe != null && xiaYe.Amount > 0)
            await DeniaResourceState.GainDarkCore(creature, (int)xiaYe.Amount, applier, source);

        var kaNiu = creature.GetPower<DeniaKaNiuPower>();
        if (kaNiu != null && kaNiu.Amount > 0)
            await PlayerCmd.GainEnergy((int)kaNiu.Amount, creature.Player);

        var amaro = creature.GetPower<DeniaAmaroPower>();
        if (amaro != null && amaro.Amount > 0)
            await CardPileCmd.Draw(_throwing, (int)amaro.Amount, creature.Player);
    }

    public static Creature? PickRandomEnemy(Player player)
    {
        var combatState = player.Creature.CombatState;
        var hittable = combatState.HittableEnemies;
        if (hittable.Count == 0) return null;
        return player.RunState.Rng.CombatTargets.NextItem(hittable);
    }
}
