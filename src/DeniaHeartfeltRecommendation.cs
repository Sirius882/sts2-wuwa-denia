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

public sealed class DeniaHeartfeltRecommendation : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_heartfelt_recommendation.png";

    public DeniaHeartfeltRecommendation()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "这个真心推荐",
        Description: "本场战斗中，从牌组中选择{IfUpgraded:show:7|4}张牌，附加[gold]集谐响应[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int count = IsUpgraded ? 7 : 4;
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