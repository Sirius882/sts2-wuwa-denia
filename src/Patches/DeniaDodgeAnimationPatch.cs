using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.AnimShake))]
public static class DeniaDodgeShakePatch
{
    public static bool Prefix(NCreature __instance)
    {
        if (__instance != DeniaDodgePowerPatch.DebuffTarget) return true;
        try { return !DeniaFormPatch.TryPlayDodge(__instance); }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(NCreature), "OnPowerIncreased")]
public static class DeniaDodgePowerPatch
{
    // This callback is synchronous; restore nested scopes even when the original throws.
    [ThreadStatic] internal static NCreature? DebuffTarget;

    public static void Prefix(NCreature __instance, PowerModel power, int amount, out NCreature? __state)
    {
        __state = DebuffTarget;
        DebuffTarget = null;
        try
        {
            if (power.GetTypeForAmount(amount) == PowerType.Debuff)
                DebuffTarget = __instance;
        }
        catch { }
    }

    public static void Finalizer(NCreature? __state) => DebuffTarget = __state;
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeDamageReceived))]
public static class DeniaDodgeAttackPatch
{
    public static void Postfix(ref Task __result, Creature target, ValueProp props, Creature? dealer)
    {
        if (target.Player?.Character is not Denia || dealer == target
            || !props.IsCardOrMonsterMove() || props.HasFlag(ValueProp.SkipHurtAnim)) return;
        __result = PlayAfter(__result, target);
    }

    private static async Task PlayAfter(Task original, Creature target)
    {
        await original;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node != null) DeniaFormPatch.TryPlayDodge(node);
        }
        catch { }
    }
}
