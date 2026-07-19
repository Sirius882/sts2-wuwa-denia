using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace Denia;

/// <summary>
/// 显式 mod 入口。有 [ModInitializer] 时 ModManager 不再 fallback PatchAll，
/// 必须在此自行 Harmony.PatchAll，并在加载时完成规则注册。
/// 形态 overlay 仍在 NCreature._Ready（需要节点树）。
/// </summary>
[ModInitializer(nameof(Init))]
public static class DeniaEntry
{
    private static Harmony? _harmony;

    public static void Init()
    {
        _harmony = new Harmony("sts2.denia");
        _harmony.PatchAll(typeof(DeniaEntry).Assembly);

        DeniaRelicBurstHandler.Init();
        DeniaMeltProtectPatch.Init();
        DeniaShroudedStarDamagePatch.Init();
        DeniaBuffTracker.Init();
    }
}
