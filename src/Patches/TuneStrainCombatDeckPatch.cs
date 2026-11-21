using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

namespace TuneStrain.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
internal static class TuneStrainCombatDeckPatch
{
    // 联机同步会重建远端牌组，而本地牌组沿用原对象。临时关键词不随牌组序列化，
    // 所以只依赖上一场的结束回调时，跳过结算的换场可能让两端复制出的战斗牌不同。
    private static void Prefix(Player __instance) => TuneStrainState.ClearDeckTemporaryResponses(__instance);
}
