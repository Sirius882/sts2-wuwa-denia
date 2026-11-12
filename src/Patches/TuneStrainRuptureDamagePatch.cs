#nullable enable
using System.Threading;
using AemeathWw.Scripts;
using HarmonyLib;

namespace TuneStrain.Patches;

[HarmonyPatch(typeof(AemeathRuptureDamageRule), nameof(AemeathRuptureDamageRule.Apply))]
public static class TuneStrainRuptureDamagePatch
{
    public static void Prefix() => TuneStrainRuptureDamageScope.Enter();

    // 原异步方法已捕获标识；返回 Task 时恢复调用方，异常退出也清理，避免污染后续攻击。
    public static void Finalizer() => TuneStrainRuptureDamageScope.Exit();
}

internal static class TuneStrainRuptureDamageScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsActive => Depth.Value > 0;

    public static void Enter() => Depth.Value++;

    public static void Exit()
    {
        if (Depth.Value > 0)
            Depth.Value--;
    }
}
