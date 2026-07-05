#nullable enable

using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Denia;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class DeniaCardFrameMaterialPatch
{
    private static readonly AccessTools.FieldRef<NCard, TextureRect> FrameRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");

    private static void Postfix(NCard __instance)
    {
        if (__instance.Model?.Pool is not DeniaCardPool)
            return;

        TextureRect frame = FrameRef(__instance);
        frame.Material = null;
    }
}