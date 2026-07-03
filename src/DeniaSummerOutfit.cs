#nullable enable
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSummerOutfit : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_summer_outfit.png";

    public DeniaSummerOutfit()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "夏日服饰？！",
        Description: "每次附加[gold]偏谐[/gold]时，额外附加{IfUpgraded:show:3|2}点。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaSummerOutfitPower>(ctx, Owner.Creature, IsUpgraded ? 3m : 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeniaSummerOutfitPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_summer_outfit_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_summer_outfit_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "夏日服饰？！",
        Description: "每次附加偏谐时，额外附加{Amount}点。",
        SmartDescription: "每次附加偏谐时，额外附加{Amount}点。");
}

[HarmonyPatch]
public static class DeniaSummerOutfitOffTunePatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(AemeathDetuneState), "TryAccumulateOffTune",
            new[] { typeof(Creature), typeof(int), typeof(Creature), typeof(CardModel) });
    }

    public static void Prefix(ref int amount, Creature applier)
    {
        if (amount <= 0 || applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaSummerOutfitPower>();
        if (pwr != null) amount += (int)pwr.Amount;
    }
}