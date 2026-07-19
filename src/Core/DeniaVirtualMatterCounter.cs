#nullable enable

using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace Denia;

/// <summary>
/// 战斗界面虚质(VirtualMatter)计数器，显示在能量计数器旁边。
/// 虚质无上限，仅显示当前数值。
/// </summary>
public sealed partial class DeniaVirtualMatterCounter : Control
{
    private const string VirtualMatterIconTexturePath = "res://images/ui/combat/denia_virtual_matter.png";
    private const float CounterSize = 58f;
    private static readonly Color ActiveModulate = Colors.White;

    private Player _player = null!;
    private Label _countLabel = null!;
    private TextureRect _icon = null!;
    private int _displayedValue = int.MinValue;

    public static DeniaVirtualMatterCounter Create(Player player)
    {
        var counter = new DeniaVirtualMatterCounter
        {
            Name = nameof(DeniaVirtualMatterCounter),
            MouseFilter = MouseFilterEnum.Stop,
            Position = new Vector2(-52f, 44f),
            Size = new Vector2(CounterSize, CounterSize),
            CustomMinimumSize = new Vector2(CounterSize, CounterSize),
            ZIndex = 10
        };
        counter._player = player;
        return counter;
    }

    public override void _Ready()
    {
        _icon = new TextureRect
        {
            Name = "VirtualMatterIcon",
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Modulate = ActiveModulate,
            Texture = LoadTexture(VirtualMatterIconTexturePath)
        };
        _icon.Set("expand_mode", 1);
        _icon.Set("stretch_mode", 5);
        AddChild(_icon);

        _countLabel = new Label
        {
            Name = "CountLabel",
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        _countLabel.AddThemeColorOverride("font_color", Colors.White);
        _countLabel.AddThemeColorOverride("font_outline_color", new Color("1E0F2A"));
        _countLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_countLabel);

        MouseEntered += OnHovered;
        MouseExited += OnUnhovered;

        UpdateValue(force: true);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        NHoverTipSet.Remove(this);
    }

    public override void _Process(double delta)
    {
        if (!IsInsideTree()) return;
        UpdateValue(force: false);
    }

    private void UpdateValue(bool force)
    {
        if (_player?.Creature == null) return;
        int current = DeniaResourceState.GetVirtualMatter(_player.Creature);
        if (!force && current == _displayedValue) return;
        _displayedValue = current;
        _countLabel.Text = $"{current}";
    }

    private void OnHovered()
    {
        var hover = NHoverTipSet.CreateAndShow(this, DeniaHoverTipHelper.CreateVirtualMatterHoverTip());
        hover.GlobalPosition = GlobalPosition + new Vector2(-34f, -220f);
    }

    private void OnUnhovered()
    {
        NHoverTipSet.Remove(this);
    }

    private static Texture2D? LoadTexture(string path)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        if (texture != null) return texture;
        Image image = Image.LoadFromFile(path);
        if (image.GetWidth() > 0 && image.GetHeight() > 0)
            return ImageTexture.CreateFromImage(image);
        return null;
    }
}
