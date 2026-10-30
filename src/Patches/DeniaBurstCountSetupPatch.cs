using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace Denia;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
public static class DeniaBurstCountSetupPatch
{
    public static void Postfix(ref Task __result, ICombatState? combatState) =>
        __result = Setup(__result, combatState);

    private static async Task Setup(Task original, ICombatState? combatState)
    {
        await original;
        if (combatState == null) return;
        foreach (var player in combatState.Players)
            if (player.Creature.GetPower<DeniaBurstCountPower>() == null)
                await PowerCmd.Apply<DeniaBurstCountPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, player.Creature, null!);
    }
}
