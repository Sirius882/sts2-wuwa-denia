#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using TuneStrain;

namespace Denia;

public sealed class DeniaRoyFairyTale : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_roy_fairy_tale.png";

    public DeniaRoyFairyTale()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "《罗伊族童话故事》",
            Description: "从牌组中选择{IfUpgraded:show:7|4}张牌，附加[color=#9A6A18]集谐响应[/color]。\n虚质强化：选择的牌数+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int count = IsUpgraded ? 7 : 4;
        if (await TrySpendVirtualMatter(play))
            count += 2;

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
        var selected = await CardSelectCmd.FromDeckGeneric(
            Owner,
            prefs,
            card => card != DeckVersion
                && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
                && !TuneStrainState.HasTemporaryResponse(card));

        foreach (var card in selected.ToList())
            TuneStrainState.AddTemporaryResponse(Owner, card);
    }

    protected override void OnUpgrade() { }
}