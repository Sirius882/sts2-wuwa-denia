using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Denia;

/// <summary>
/// Archaic Tooth 转化："请您不要···" → "最初和最后的礼物"。
/// 主要转化逻辑通过 ITranscendenceCard 接口（BaseLib 官方机制）处理。
/// 此补丁额外接管 SetupForPlayer 和 AfterObtained，确保工具提示和转化动画正确。
/// </summary>

[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
public static class DeniaArchaicToothSetupPatch
{
    [HarmonyPostfix]
    private static void Postfix(ArchaicTooth __instance, Player player, ref bool __result)
    {
        if (__result) return;
        CardModel? starter = FindDeniaTranscendenceStarter(player);
        if (starter == null) return;
        CardModel transformed = BuildDeniaAncientCard(starter);
        __instance.SetupForTests(starter.ToSerializable(), transformed.ToSerializable());
        __result = true;
    }

    private static CardModel? FindDeniaTranscendenceStarter(Player player)
    {
        var starterId = ModelDb.Card<DeniaPleaseDoNot>().Id;
        return player.Deck.Cards.FirstOrDefault(c => c.Id == starterId);
    }

    private static CardModel BuildDeniaAncientCard(CardModel starter)
    {
        CardModel canonical = ModelDb.Card<DeniaFirstLastGift>();
        CardModel transformed = starter.Owner.RunState.CreateCard(canonical, starter.Owner);
        for (int i = 0; i < starter.CurrentUpgradeLevel && transformed.IsUpgradable; i++)
            CardCmd.Upgrade(transformed);
        if (starter.Enchantment != null)
        {
            EnchantmentModel enchantment = (EnchantmentModel)starter.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, transformed, enchantment.Amount);
        }
        return transformed;
    }
}
[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
public static class DeniaArchaicToothAfterObtainedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ArchaicTooth __instance, ref Task __result)
    {
        var starterId = ModelDb.Card<DeniaPleaseDoNot>().Id;
        CardModel? starter = __instance.Owner.Deck.Cards.FirstOrDefault(c => c.Id == starterId);
        if (starter == null) return true;
        __result = HandleAfterObtained(starter);
        return false;
    }

    private static async Task HandleAfterObtained(CardModel starter)
    {
        CardModel canonical = ModelDb.Card<DeniaFirstLastGift>();
        CardModel transformed = starter.Owner.RunState.CreateCard(canonical, starter.Owner);
        for (int i = 0; i < starter.CurrentUpgradeLevel && transformed.IsUpgradable; i++)
            CardCmd.Upgrade(transformed);
        if (starter.Enchantment != null)
        {
            EnchantmentModel enchantment = (EnchantmentModel)starter.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, transformed, enchantment.Amount);
        }
        await CardCmd.Transform(starter, transformed);
    }
}
