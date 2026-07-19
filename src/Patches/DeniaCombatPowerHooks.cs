using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Denia;

/// <summary>虚质科学直觉：消耗虚质后累加（熵变/松子已改走 Power.AfterPowerAmountChanged 实时结算）。</summary>
[HarmonyPatch(typeof(DeniaResourceState), nameof(DeniaResourceState.TrySpendVirtualMatter))]
public static class DeniaVMIntuitionPatch
{
    private static void Postfix(ref Task<bool> __result, Creature creature, int amount)
    {
        if (amount <= 0) return;
        __result = Wrap(__result, creature, amount);
    }

    private static async Task<bool> Wrap(Task<bool> original, Creature creature, int amount)
    {
        bool spent = await original;
        if (spent)
            await DeniaVirtualScienceIntuitionPower.AccumulateVM(creature, amount);
        return spent;
    }
}
