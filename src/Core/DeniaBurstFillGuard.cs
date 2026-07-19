using System;

namespace Denia;

/// <summary>
/// 无条件“填层引爆”期间抑制「回到远方 / 从远方」等附加聚爆联动。
/// 熔毁殆尽、深黯·终末·恒常 等卡共用此守卫。
/// </summary>
public static class DeniaBurstFillGuard
{
    public static bool IsActive { get; private set; }

    public static IDisposable Enter()
    {
        IsActive = true;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsActive = false;
        }
    }
}
