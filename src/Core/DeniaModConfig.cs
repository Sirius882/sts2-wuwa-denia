using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace Denia;

internal sealed class DeniaModConfig : SimpleModConfig
{
    public static bool UseCodeAnimation { get; set; }

    public override void SetupConfigUI(Control optionContainer)
    {
        base.SetupConfigUI(optionContainer);
        var row = optionContainer.GetNode<Control>(nameof(UseCodeAnimation));
        var label = row.GetNode<MegaRichTextLabel>("Label");
        var textArea = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        textArea.AddThemeConstantOverride("margin_right", 192);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(0, 94);
        row.AddChild(textArea);
        row.MoveChild(textArea, 0);
        label.Reparent(textArea, false);
    }
}
