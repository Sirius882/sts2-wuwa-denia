using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.AnimShake))]
public static class DeniaDodgeShakePatch
{
    public static bool Prefix(NCreature __instance)
    {
        try { return !DeniaFormPatch.TryPlayDodge(__instance); }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class DeniaDodgeHitPatch
{
    public static bool Prefix(NCreature __instance, string trigger)
    {
        if (trigger != "Hit") return true;
        try { return !DeniaFormPatch.TryPlayDodge(__instance); }
        catch { return true; }
    }
}
