using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Denia;

/// <summary>Bounded background NV12 decode using Windows Media Foundation/D3D11, with GPU YUV conversion.</summary>
internal sealed class DeniaHardwareVideo : IDisposable
{
    internal sealed class Frame(int width, int height)
    {
        public readonly byte[] Luma = new byte[width * height];
        public readonly byte[] Chroma = new byte[width * height / 2];
        public double Time;
        public bool Hardware;
    }

    // The authored file is 397 frames at 60 Hz; keep its original duration and cadence.
    internal const double Duration = 397.0 / 60;
    private readonly BlockingCollection<Frame> _free = new(4);
    private readonly ConcurrentQueue<Frame> _ready = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private Frame? _next;
    private Exception? _error;
    private volatile bool _opened;
    internal bool Opened => _opened;
    internal Exception? Error => _error;
    internal int Width { get; private set; }
    internal int Height { get; private set; }
    internal int Presented { get; private set; }
    internal int Skipped { get; private set; }
    internal bool Hardware { get; private set; }
    internal double Position { get; private set; }
    private int _decoded;
    internal int Decoded => Volatile.Read(ref _decoded);
    private static readonly object LibraryLock = new();
    private static IntPtr _library;
    private static OpenNative _open = null!;
    private static ReadNative _read = null!;
    private static RewindNative _rewind = null!;
    private static CloseNative _close = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int OpenNative([MarshalAs(UnmanagedType.LPWStr)] string path, out IntPtr decoder, out int width, out int height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReadNative(IntPtr decoder, byte[] y, int yCapacity, byte[] uv, int uvCapacity, out long timestamp, out int hardware);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RewindNative(IntPtr decoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CloseNative(IntPtr decoder);

    internal DeniaHardwareVideo()
    {
        // Resolve Godot paths on the main thread. Extraction, COM and decode run only on the worker.
        string directory = ProjectSettings.GlobalizePath("user://denia/video");
        _worker = Task.Run(() => Decode(directory));
    }

    private static string Materialize(string directory, string name, string extension, byte[] bytes)
    {
        Directory.CreateDirectory(directory);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string path = Path.Combine(directory, name + "_" + hash + extension);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return path;
    }

    private void Decode(string directory)
    {
        IntPtr decoder = IntPtr.Zero;
        try
        {
            byte[] library = FileAccess.GetFileAsBytes("res://native/denia_video.bin");
            byte[] source = FileAccess.GetFileAsBytes("res://videos/denia_character_select_bg.mp4");
            if (library.Length == 0 || source.Length == 0) throw new IOException("Hardware video assets are missing from Denia PCK.");
            string libraryPath = Materialize(directory, "denia_video", ".dll", library);
            string sourcePath = Materialize(directory, "denia_select", ".mp4", source);
            lock (LibraryLock)
            {
                if (_library == IntPtr.Zero)
                {
                    _library = NativeLibrary.Load(libraryPath);
                    _open = Marshal.GetDelegateForFunctionPointer<OpenNative>(NativeLibrary.GetExport(_library, "denia_video_open"));
                    _read = Marshal.GetDelegateForFunctionPointer<ReadNative>(NativeLibrary.GetExport(_library, "denia_video_read"));
                    _rewind = Marshal.GetDelegateForFunctionPointer<RewindNative>(NativeLibrary.GetExport(_library, "denia_video_rewind"));
                    _close = Marshal.GetDelegateForFunctionPointer<CloseNative>(NativeLibrary.GetExport(_library, "denia_video_close"));
                }
            }
            Marshal.ThrowExceptionForHR(_open(sourcePath, out decoder, out int width, out int height));
            if (width != 3840 || height != 2160) throw new InvalidDataException("Unexpected character-select dimensions.");
            Width = width; Height = height;
            for (int i = 0; i < 4; i++) _free.Add(new Frame(width, height));
            _opened = true;
            long loop = 0;
            while (!_stop.IsCancellationRequested)
            {
                Frame frame = _free.Take(_stop.Token);
                int hr = _read(decoder, frame.Luma, frame.Luma.Length, frame.Chroma, frame.Chroma.Length, out long pts, out int hardware);
                if (hr == 1)
                {
                    _free.Add(frame);
                    Marshal.ThrowExceptionForHR(_rewind(decoder));
                    loop++;
                    continue;
                }
                Marshal.ThrowExceptionForHR(hr);
                frame.Time = pts / 10_000_000.0 + loop * Duration;
                frame.Hardware = hardware != 0;
                _ready.Enqueue(frame);
                Interlocked.Increment(ref _decoded);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _error = ex; }
        finally
        {
            // SourceReader/device/COM must be released on their creating MTA thread.
            if (decoder != IntPtr.Zero) _close(decoder);
        }
    }

    internal Frame? Take(double clock)
    {
        Frame? selected = null;
        while (true)
        {
            if (_next == null && !_ready.TryDequeue(out _next)) break;
            if (_next.Time > clock + 0.0001) break;
            if (selected != null) { _free.Add(selected); Skipped++; }
            selected = _next;
            _next = null;
        }
        if (selected != null)
        {
            Position = selected.Time;
            Hardware = selected.Hardware;
            Presented++;
        }
        return selected;
    }

    internal void Release(Frame frame) => _free.Add(frame);

    public void Dispose()
    {
        // Never join a codec/GPU operation on the game's main thread.
        _stop.Cancel();
        _worker.ContinueWith(_ => { _free.Dispose(); _stop.Dispose(); }, TaskScheduler.Default);
    }
}
