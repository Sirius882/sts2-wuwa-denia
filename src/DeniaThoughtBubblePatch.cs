// 文件说明：把达妮娅复用星辉资源时的"辉星不足"思考气泡改写为"黯核不足"。
#nullable enable

using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Denia;

[HarmonyPatch(typeof(NThoughtBubbleVfx), nameof(NThoughtBubbleVfx.Create), new[] { typeof(string), typeof(Creature), typeof(double?) })]
public static class DeniaThoughtBubblePatch
{
    private const string NotEnoughStarsKey = "NOT_ENOUGH_STARS";

    private static void Prefix(ref string text, Creature speaker)
    {
        if (speaker.Player?.Character is not Denia)
            return;

        string defaultStarsText = new LocString("combat_messages", NotEnoughStarsKey).GetFormattedText() ?? string.Empty;
        if (text != defaultStarsText)
            return;

        // Denia 未注册本地化 key，直接硬编码替换文本。
        text = "黯核不足";
    }
}
