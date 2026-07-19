// 文件日志：用于捕获游戏闪退前的错误信息。
// 输出到 E:\STS2_mod\denia_crash.log
#nullable enable

using System;
using System.IO;
using Godot;

namespace Denia;

public static class DeniaCrashLog
{
    private static readonly string LogPath = @"E:\STS2_mod\denia_crash.log";
    private static readonly object _lock = new();

    static DeniaCrashLog()
    {
        try { File.WriteAllText(LogPath, $"=== Denia Crash Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===\n"); }
        catch (Exception ex) { GD.Print($"[Denia] CrashLog init failed: {ex.Message}"); }
    }

    public static void Write(string message)
    {
        try
        {
            lock (_lock) { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); }
        }
        catch (Exception ex) { GD.PrintErr($"[Denia] CrashLog write failed: {ex.Message}"); }
    }

    public static void Write(Exception ex, string context)
    {
        Write($"EXCEPTION in {context}: {ex.GetType().Name}: {ex.Message}");
        Write($"  Stack: {ex.StackTrace}");

        // Inner exception
        var inner = ex.InnerException;
        int depth = 0;
        while (inner != null && depth < 10)
        {
            Write($"  Inner[{depth}]: {inner.GetType().Name}: {inner.Message}");
            Write($"  Inner[{depth}] Stack: {inner.StackTrace}");
            inner = inner.InnerException;
            depth++;
        }
    }
}
