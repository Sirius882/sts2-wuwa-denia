using System;
using Godot;

namespace Denia;

/// <summary>选人视频背景：原比例铺满、原速循环，首帧占位并随界面显隐暂停解码。</summary>
public partial class DeniaCharacterSelectBackground : Control
{
    private VideoStreamPlayer _video = null!;
    private TextureRect _poster = null!;
    private bool _decoded;
    private TextureRect _hardwareView = null!;
    private DeniaHardwareVideo? _hardware;
    private Image? _lumaImage, _chromaImage;
    private ImageTexture? _lumaTexture, _chromaTexture;
    private ShaderMaterial? _nv12Material;
    private double _clock;
    private bool _clockStarted;
    private bool _fallback;
    private bool _reported;

    public Godot.Collections.Dictionary GetPlaybackStats() => new()
    {
        ["backend"] = _fallback ? "Theora fallback" : "MediaFoundation D3D11 NV12",
        ["playing"] = IsVisibleInTree() && (_clockStarted || _fallback && _video.IsPlaying()),
        ["position"] = _fallback ? _video.StreamPosition : _clock % DeniaHardwareVideo.Duration,
        ["presented"] = _hardware?.Presented ?? 0,
        ["decoded"] = _hardware?.Decoded ?? 0,
        ["skipped"] = _hardware?.Skipped ?? 0,
        ["hardware"] = _hardware?.Hardware ?? false,
        ["visible"] = _fallback ? _video.Visible : _hardwareView.Visible,
        ["width"] = _hardware?.Width ?? 3840,
        ["height"] = _hardware?.Height ?? 2160
    };

    public override void _Ready()
    {
        _video = GetNode<VideoStreamPlayer>("Video");
        _poster = GetNode<TextureRect>("Poster");
        _hardwareView = GetNode<TextureRect>("HardwareVideo");
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Media Foundation needs Windows.");
            _hardware = new DeniaHardwareVideo();
        }
        catch (Exception ex) { UseFallback(ex); }
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        Resized += UpdateLayout;
        VisibilityChanged += SyncPlayback;
        FitViewport();
        UpdateLayout();
        SyncPlayback();
    }

    private void UpdateLayout()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        float scale = Mathf.Max(Size.X / 3840, Size.Y / 2160);
        Vector2 size = new Vector2(3840, 2160) * scale;
        _video.Size = _poster.Size = _hardwareView.Size = size;
        _video.Position = _poster.Position = _hardwareView.Position = (Size - size) * 0.5f;
    }

    private void FitViewport()
    {
        // 原AnimatedBg有额外留边、1.1倍缩放与鼠标视差；视频自身已构图，抵消父级变换。
        // 仍留在原背景层级，不使用TopLevel或CanvasLayer，以免盖住选人按钮与正文。
        if (GetParent() is CanvasItem parent)
        {
            Transform2D transform = parent.GetGlobalTransform();
            Scale = Vector2.One / transform.Scale;
            Rotation = -transform.Rotation;
        }
        GlobalPosition = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    private void SyncPlayback()
    {
        if (!_fallback) return;
        if (IsVisibleInTree())
        {
            _video.Paused = false;
            if (!_video.IsPlaying()) _video.Play();
        }
        else _video.Paused = true;
    }

    public override void _Process(double delta)
    {
        FitViewport();
        if (!_fallback)
        {
            if (_hardware?.Error is Exception error) { UseFallback(error); return; }
            if (!IsVisibleInTree() || _hardware == null) return;
            if (_clockStarted) _clock += delta;
            DeniaHardwareVideo.Frame? frame = _hardware.Take(_clock);
            if (frame == null) return;
            try
            {
                if (!_clockStarted) { _clockStarted = true; _clock = frame.Time; }
                Present(frame);
            }
            finally { _hardware.Release(frame); }
            return;
        }
        if (!IsVisibleInTree())
        {
            _video.Paused = true;
            return;
        }
        _video.Paused = false;
        if (!_video.IsPlaying()) _video.Play();
        // 解码出第一幅画面后才替换同一视频首帧占位，避免刚选中时闪黑。
        if (!_decoded && _video.StreamPosition > 0)
        {
            _decoded = true;
            _video.Show();
            _poster.Hide();
        }
    }

    private void Present(DeniaHardwareVideo.Frame frame)
    {
        // Only upload the two NV12 planes. Color conversion runs in the GPU shader, not CPU RGB copies.
        const int width = 3840, height = 2160;
        if (_lumaImage == null)
        {
            _lumaImage = Image.CreateFromData(width, height, false, Image.Format.R8, frame.Luma);
            _chromaImage = Image.CreateFromData(width / 2, height / 2, false, Image.Format.Rg8, frame.Chroma);
            _lumaTexture = ImageTexture.CreateFromImage(_lumaImage);
            _chromaTexture = ImageTexture.CreateFromImage(_chromaImage);
            _nv12Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/denia_video_nv12.gdshader") };
            _nv12Material.SetShaderParameter("chroma", _chromaTexture);
            _hardwareView.Texture = _lumaTexture;
            _hardwareView.Material = _nv12Material;
        }
        else
        {
            _lumaImage.SetData(width, height, false, Image.Format.R8, frame.Luma);
            _chromaImage!.SetData(width / 2, height / 2, false, Image.Format.Rg8, frame.Chroma);
            _lumaTexture!.Update(_lumaImage);
            _chromaTexture!.Update(_chromaImage);
        }
        _hardwareView.Show();
        _poster.Hide();
        if (!_reported)
        {
            GD.Print($"[Denia] Character background: Media Foundation / D3D11, hardware surfaces={frame.Hardware}, 3840x2160 60 FPS.");
            _reported = true;
        }
    }

    private void UseFallback(Exception error)
    {
        _hardware?.Dispose(); _hardware = null;
        _fallback = true;
        _hardwareView?.Hide();
        GD.PushWarning($"[Denia] Hardware character background unavailable, using Theora: {error.Message}");
        if (IsNodeReady()) SyncPlayback();
    }

    public override void _ExitTree()
    {
        _hardware?.Dispose(); _hardware = null;
        if (GodotObject.IsInstanceValid(_video)) _video.Stop();
        _hardwareView.Texture = null;
        _hardwareView.Material = null;
        _nv12Material?.Dispose(); _nv12Material = null;
        _lumaTexture?.Dispose(); _lumaTexture = null;
        _chromaTexture?.Dispose(); _chromaTexture = null;
        _lumaImage?.Dispose(); _lumaImage = null;
        _chromaImage?.Dispose(); _chromaImage = null;
    }
}
