#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaWorthlessIndividual : DeniaCard, IResonanceBreakCard
{
    public override int CurrentVirtualMatterCost => 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_worthless_individual.png";

    public DeniaWorthlessIndividual()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "“没有价值的个体”",
            Description: "给任意{IfUpgraded:show:3|1}张手牌附加[color=#9A6A18]集谐响应[/color]。无条件[color=#9A6A18]谐度破坏[/color]。\n虚质强化：附加2层[color=#9A6A18]易伤[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        // 先选牌附加集谐响应，再触发谐度破坏（策划要求顺序调整）。
        int count = IsUpgraded ? 3 : 1;
        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse))
            .ToList();
        count = Math.Min(count, eligible.Count);
        if (count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
            var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs,
                card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse), this);
            foreach (var card in selected.ToList())
                TuneStrainState.AddTemporaryResponse(Owner, card);
        }

        await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
            await PowerCmd.Apply<VulnerablePower>(ctx, play.Target, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
