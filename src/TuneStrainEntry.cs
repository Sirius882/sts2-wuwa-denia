using System;
using System.Runtime.CompilerServices;
using TuneStrain;

namespace TuneStrain.EntryPoint;

/// <summary>
/// 内嵌 TuneStrain 的引导程序。
/// beta 中 TuneStrain 与 Denia 共用一个程序集，因此由 DeniaEntry 的唯一 ModInitializer 调用。
/// 这里只初始化静态状态和日志，绝不单独创建 Harmony 或重复 PatchAll。
/// </summary>
internal static class TuneStrainEntry
{
    private static readonly object InitializationLock = new();
    private static bool _initialized;

    internal static void Bootstrap()
    {
        lock (InitializationLock)
        {
            if (_initialized)
                return;

            // typeof(...) 本身不会保证运行静态构造；必须显式执行，
            // 以便注册 CombatEnded 生命周期回调并早于任何合并程序集补丁触发。
            RuntimeHelpers.RunClassConstructor(typeof(TuneStrainState).TypeHandle);

            TuneStrainLog.Info("tune_strain initialized: 集谐·偏移 / 干涉 / 响应 系统已就绪。");

            // 只有静态状态和日志均完成后才提交幂等状态；异常不会留下半初始化标记。
            _initialized = true;
        }
    }
}
