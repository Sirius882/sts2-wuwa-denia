using System;
using System.IO;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Denia;

/// <summary>按共用动作时钟随机读取透明视频帧，含裁剪后的相对脚底坐标。</summary>
internal sealed class DeniaBattleVideo : IDisposable
{
    private const uint Magic = 0x32564244; // DBV2, little endian
    private const float ReferenceHeight = 432;
    private readonly record struct Frame(uint Time, int X, int Y, uint Size, ulong Offset);
    private readonly FileAccess _file;
    private readonly Frame[] _frames;
    private readonly Image _image = new();
    private readonly ImageTexture _texture = new();
    private int _index = -1;
    private Vector2I _size;
    private readonly ulong _audioOffset;
    private readonly uint _audioBytes, _reverseBytes;
    private AudioStreamOggVorbis? _audio, _reversed;
    internal double Duration { get; }
    internal double[] Impacts { get; }
    internal Sprite2D Sprite { get; } = new() { Centered = false, ShowBehindParent = true };

    internal static string PathFor(string clip) => $"res://videos/{clip}.dbv";

    internal static DeniaBattleVideo? TryOpen(string clip, float layoutHeight)
    {
        var path = PathFor(clip);
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;
        try
        {
            ulong length = file.GetLength();
            if (length < 24 || file.Get32() != Magic) throw new InvalidDataException(path);
            uint count = file.Get32();
            uint duration = file.Get32(), impactCount = file.Get32();
            uint audio = file.Get32(), reversed = file.Get32();
            ulong previousEnd = 24 + 4UL * impactCount + 24UL * count;
            if (count == 0 || count > 4096 || duration == 0 || impactCount > 4
                || length < previousEnd + audio + reversed) throw new InvalidDataException(path);
            var impacts = new double[impactCount];
            for (int i = 0; i < impacts.Length; i++)
            {
                uint time = file.Get32();
                if (time == 0 || time >= duration || (i > 0 && impacts[i - 1] >= time / 1_000_000.0))
                    throw new InvalidDataException(path);
                impacts[i] = time / 1_000_000.0;
            }
            var frames = new Frame[count];
            ulong endOfVideo = length - audio - reversed;
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = new Frame(file.Get32(), unchecked((int)file.Get32()), unchecked((int)file.Get32()),
                    file.Get32(), file.Get64());
                if ((i == 0 ? frame.Time != 0 : frame.Time <= frames[i - 1].Time)
                    || frame.Offset != previousEnd || frame.Offset > endOfVideo || frame.Size == 0 || frame.Size > endOfVideo - frame.Offset || frame.Time >= duration)
                    throw new InvalidDataException(path);
                previousEnd = frame.Offset + frame.Size;
                frames[i] = frame;
            }
            if (previousEnd != endOfVideo || Array.Exists(impacts, impact =>
                !Array.Exists(frames, frame => frame.Time == (uint)Math.Round(impact * 1_000_000))))
                throw new InvalidDataException(path);
            var video = new DeniaBattleVideo(file, frames, layoutHeight, duration, impacts, endOfVideo, audio, reversed);
            try { video.Seek(0, false); }
            catch { video.Sprite.Free(); video.Dispose(); throw; }
            return video;
        }
        catch (Exception exception)
        {
            if (GodotObject.IsInstanceValid(file)) file.Dispose();
            GD.PrintErr($"[DeniaBattleVideo] {clip}: {exception.Message}");
            return null;
        }
    }

    private DeniaBattleVideo(FileAccess file, Frame[] frames, float layoutHeight, uint duration,
        double[] impacts, ulong audioOffset, uint audio, uint reversed)
    {
        _file = file;
        _frames = frames;
        Duration = duration / 1_000_000.0;
        Impacts = impacts;
        _audioOffset = audioOffset;
        _audioBytes = audio;
        _reverseBytes = reversed;
        Sprite.Scale = Vector2.One * (layoutHeight / ReferenceHeight);
        Sprite.Texture = _texture;
    }

    internal void Seek(double seconds, bool flip)
    {
        // The table uses microseconds. Round like the converter so a clamped impact clock selects its exact frame.
        uint time = (uint)Math.Round(seconds * 1_000_000);
        int left = 0, right = _frames.Length;
        while (left + 1 < right)
        {
            int middle = (left + right) / 2;
            if (_frames[middle].Time <= time) left = middle;
            else right = middle;
        }
        var frame = _frames[left];
        if (left != _index)
        {
            _file.Seek(frame.Offset);
            if (_image.LoadWebpFromBuffer(_file.GetBuffer(frame.Size)) != Error.Ok)
                throw new InvalidDataException($"Invalid combat video frame {left}");
            if (_image.GetFormat() != Image.Format.Rgba8) _image.Convert(Image.Format.Rgba8);
            Vector2I size = _image.GetSize();
            if (size == _size) _texture.Update(_image);
            else { _texture.SetImage(_image); _size = size; }
            _index = left;
        }
        SetFacing(flip);
    }

    internal void SetFacing(bool flip)
    {
        var frame = _frames[_index];
        Sprite.FlipH = flip;
        Sprite.Position = new Vector2(flip ? -frame.X - _size.X : frame.X, frame.Y) * Sprite.Scale;
    }

    internal AudioStreamOggVorbis? GetAudio(bool reversed)
    {
        uint length = reversed ? _reverseBytes : _audioBytes;
        if (length == 0) return null;
        ref var stream = ref (reversed ? ref _reversed : ref _audio);
        if (stream == null)
        {
            _file.Seek(_audioOffset + (reversed ? _audioBytes : 0));
            stream = AudioStreamOggVorbis.LoadFromBuffer(_file.GetBuffer(length));
        }
        return stream;
    }

    public void Dispose()
    {
        _file.Dispose();
        _image.Dispose();
        _texture.Dispose();
    }
}
