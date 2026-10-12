using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using TuneStrain.EntryPoint;

namespace Denia;

/// <summary>
/// 显式 mod 入口。有 [ModInitializer] 时 ModManager 不再 fallback PatchAll，
/// 必须在此自行 Harmony.PatchAll，并在加载时完成规则注册。
/// 形态 overlay 仍在 NCreature._Ready（需要节点树）。
/// </summary>
[ModInitializer(nameof(Init))]
public static class DeniaEntry
{
    private static readonly object InitializationLock = new();
    private static Harmony? _harmony;
    private static bool _initialized;

    public static void Init()
    {
        lock (InitializationLock)
        {
            // 合并后的 denia.dll 只有此处是 ModInitializer；防止 ModManager 重入时重复注册 Harmony 补丁。
            if (_initialized)
                return;

            // TuneStrain 已内嵌进 beta 的 denia.dll。先确保其战斗生命周期状态完成初始化，
            // 再对整个合并程序集统一 PatchAll 一次。
            TuneStrainEntry.Bootstrap();

            _harmony = new Harmony("sts2.denia");
            _harmony.PatchAll(typeof(DeniaEntry).Assembly);

            DeniaRelicBurstHandler.Init();
            DeniaMeltProtectPatch.Init();
            DeniaShroudedStarDamagePatch.Init();
            DeniaBuffTracker.Init();

            // 仅当 TuneStrain、Harmony 和所有 Denia 规则均成功初始化后才提交幂等状态。
            // 若其中任一步骤抛异常，后续 ModManager 调用仍可重新尝试完整初始化。
            _initialized = true;
        }
    }
}
