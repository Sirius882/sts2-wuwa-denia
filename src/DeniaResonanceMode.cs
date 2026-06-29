using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaResonanceMode : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { DeniaSpecialKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_resonance_mode.png";

    public DeniaResonanceMode()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "共鸣模态·集谐",
        Description: "进入[gold]共鸣模态·集谐[/gold]，给任意两张手牌附加[gold]集谐响应[/gold]。\n[gold]共鸣模态·集谐[/gold]：基础、普通和罕见稀有度的卡牌，能附加的最高集谐层数+1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaResonanceModePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && !card.Keywords.Contains(DeniaSpecialKeywords.TuneStrainResponse))
            .ToList();
        int count = System.Math.Min(2, eligible.Count);
        if (count <= 0) return;

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs,
            card => card != this && !card.Keywords.Contains(DeniaSpecialKeywords.TuneStrainResponse), this);
        foreach (var card in selected.ToList())
            card.AddKeyword(DeniaSpecialKeywords.TuneStrainResponse);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeniaResonanceModePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "共鸣模态·集谐",
            Description: "基础、普通和罕见稀有度的卡牌，能附加的最高集谐层数+1。",
            SmartDescription: "基础、普通和罕见稀有度的卡牌，能附加的最高集谐层数+1。");
}

public static class DeniaResonanceModeHelper
{
    public static bool IsActive(Creature? creature) =>
        creature?.GetPower<DeniaResonanceModePower>() != null;
}
