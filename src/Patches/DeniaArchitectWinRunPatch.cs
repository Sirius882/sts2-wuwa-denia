#nullable enable
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

/// <summary>
/// Denia 没有建筑师对白时，直接沿用测试版原生胜利结算。
/// 优先处理游戏事件，避免其他 mod 的旧版兜底将胜利房间送入进入下一幕的投票。
/// 不访问或修改 Aemeath 的内部补丁；有对白的事件仍完整走原版演出。
/// </summary>
[HarmonyPatch(typeof(TheArchitect), "WinRun")]
internal static class DeniaArchitectWinRunPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(TheArchitect __instance, AncientDialogue? ____dialogue, ref Task __result)
    {
        var owner = __instance.Owner;
        if (owner == null || owner.Character is not Denia || ____dialogue != null)
            return true;

        __result = LocalContext.IsMe(owner)
            ? RunManager.Instance.WinRun()
            : Task.CompletedTask;
        return false;
    }
}
