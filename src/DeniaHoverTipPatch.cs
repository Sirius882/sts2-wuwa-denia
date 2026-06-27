#nullable enable

using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace Denia;

/// <summary>
/// 达妮娅悬浮提示文本拼装辅助，参考 AemeathHoverTipHelper。
/// </summary>
public static class DeniaHoverTipHelper
{
    private const string VirtualMatterIcon = "[img]res://images/ui/combat/denia_virtual_matter.png[/img]";
    private const string DarkCoreIcon = "[img]res://images/ui/combat/denia_dark_core_icon.png[/img]";

    public static HoverTip CreateVirtualMatterHoverTip()
    {
        string description = $"由粉色切换到黑色时，获得10点{VirtualMatterIcon}。粉色形态下，打出攻击牌或触发熔解/聚爆引爆时，获得2点。由黑色切换到粉色时归零；若切换前拥有7点{VirtualMatterIcon}，抽1张牌。{VirtualMatterIcon}最多不超过20。";
        return new HoverTip(new LocString("denia_ui", "virtualMatterTitle"), description);
    }

    public static HoverTip CreateDarkCoreHoverTip()
    {
        string description = $"玩家的回合开始时，如果处于粉色形态，{DarkCoreIcon}+1。{DarkCoreIcon}最多不超过5个。";
        return new HoverTip(new LocString("denia_ui", "darkCoreTitle"), description);
    }
}
/// <summary>
/// 替换 NStarCounter 的悬浮提示为黯核说明（仅达妮娅角色）。
/// </summary>
[HarmonyPatch(typeof(NStarCounter), "OnHovered")]
public static class DeniaStarCounterHoverPatch
{
    private static readonly AccessTools.FieldRef<NStarCounter, Player?> PlayerRef =
        AccessTools.FieldRefAccess<NStarCounter, Player?>("_player");

    private static bool Prefix(NStarCounter __instance)
    {
        Player? player = PlayerRef(__instance);
        if (player?.Character is not Denia)
            return true;

        var hover = NHoverTipSet.CreateAndShow(__instance, DeniaHoverTipHelper.CreateDarkCoreHoverTip());
        hover.GlobalPosition = __instance.GlobalPosition + new Godot.Vector2(-34f, -300f);
        return false;
    }
}
