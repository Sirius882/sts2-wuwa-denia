using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Commands;
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

    public static async Task MarkTemporaryResonanceMode(Creature creature, Creature applier, CardModel source)
    {
        if (creature.GetPower<DeniaPermanentResonanceModeSeenPower>() != null) return;
        if (creature.GetPower<DeniaBlackFormTemporaryResonanceModePower>() == null)
            await PowerCmd.Apply<DeniaBlackFormTemporaryResonanceModePower>(_throwing, creature, 1m, applier, source);
    }

    public static async Task SwitchToBlack(Creature creature, Creature applier, CardModel source)
    {
        await MarkFormSwitchedThisTurn(creature, applier, source);
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null)
            await PowerCmd.Apply<DeniaFormPower>(_throwing, creature, 1m, applier, source);
        else if (power.Amount <= 0)
            await PowerCmd.ModifyAmount(_throwing, power, 1m, applier, source);
        DeniaFormPatch.RefreshForCreature(creature);
        await DeniaResourceState.GainVirtualMatter(creature, 10, applier, source);
        await ApplyFormSwitchEffects(creature, applier, source);
    }

    public static async Task SwitchToPink(Creature creature, Creature applier, CardModel source, bool clearVM = true)
    {
        await MarkFormSwitchedThisTurn(creature, applier, source);
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null || power.Amount <= 0) return;


        await PowerCmd.ModifyAmount(_throwing, power, -1m, applier, source);

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

        if (clearVM)
        {
            // 切换前拥有7虚质 → 抽1张牌
            if (DeniaResourceState.GetVirtualMatter(creature) >= 7)
                await CardPileCmd.Draw(_throwing, 1, creature.Player);
            await DeniaResourceState.ClearVirtualMatter(creature, applier, source);
        }
        DeniaFormPatch.RefreshForCreature(creature);

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
