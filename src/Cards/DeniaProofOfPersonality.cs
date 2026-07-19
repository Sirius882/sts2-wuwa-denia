#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
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
public sealed class DeniaProofOfPersonality : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_proof_of_personality.png";

    public DeniaProofOfPersonality()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "人格的证明",
        Description: "每次附加[gold]集谐·偏移[/gold]时，额外附加1点。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaProofOfPersonalityPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeniaProofOfPersonalityPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_proof_of_personality_power.webp";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_proof_of_personality_power.webp";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "人格的证明",
        Description: "每次附加集谐·偏移时，额外附加{Amount}点。",
        SmartDescription: "每次附加集谐·偏移时，额外附加{Amount}点。");
}

[HarmonyPatch(typeof(TuneStrainState), nameof(TuneStrainState.TryAddBias))]
public static class DeniaProofOfPersonalityBiasPatch
{
    public static void Prefix(ref int amount, Creature applier)
    {
        if (amount <= 0 || applier?.IsPlayer != true) return;
        var pwr = applier.GetPower<DeniaProofOfPersonalityPower>();
        if (pwr != null) amount += (int)pwr.Amount;
    }
}