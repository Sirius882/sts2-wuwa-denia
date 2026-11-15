using Godot;

namespace Denia;

/// <summary>选人视频背景：原比例铺满、原速循环，首帧占位并随界面显隐暂停解码。</summary>
public partial class DeniaCharacterSelectBackground : Control
{
    private VideoStreamPlayer _video = null!;
    private TextureRect _poster = null!;
    private bool _decoded;

    public override void _Ready()
    {
        _video = GetNode<VideoStreamPlayer>("Video");
        _poster = GetNode<TextureRect>("Poster");
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
        _video.Size = _poster.Size = size;
        _video.Position = _poster.Position = (Size - size) * 0.5f;
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

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_video)) _video.Stop();
    }
}
