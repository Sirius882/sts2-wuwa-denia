using Godot;

namespace Denia;

/// <summary>忽略吞事件的纯布局Control，保留实际可交互GUI的点击区域。</summary>
public static class DeniaBlankClick
{
    public static bool IsBlank(Control? hovered)
    {
        for (Node? node = hovered; node != null; node = node.GetParent())
        {
            if (node is not Control control) continue;
            if (control.MouseFilter == Control.MouseFilterEnum.Ignore) continue;
            if (control is BaseButton button && button.Disabled) continue;
            if (control.FocusMode != Control.FocusModeEnum.None || control.HasMethod("_gui_input")
                || control.GetSignalConnectionList(Control.SignalName.GuiInput).Count > 0)
                return false;
        }
        return true;
    }
}
