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
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaWinningFace : DeniaCard, IResonanceBreakCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_winning_face.png";

    public DeniaWinningFace()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "“没有价值的个体”",
        Description: "无条件[gold]谐度破坏[/gold]。此次[gold]谐度破坏[/gold]只造成五分之一的伤害。给任意{IfUpgraded:show:3|1}张手牌附加[gold]集谐响应[/gold]。\n虚质强化：[gold]谐度破坏[/gold]伤害恢复正常。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        bool normalDamage = await TrySpendVirtualMatter(play);
        if (normalDamage)
            await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        else
            await DeniaResonanceBreakDamageModifier.RunOnce(
                0.2m,
                () => AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this));

        int count = IsUpgraded ? 3 : 1;
        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse))
            .ToList();
        count = Math.Min(count, eligible.Count);
        if (count <= 0) return;

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs,
            card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse), this);
        foreach (var card in selected.ToList())
            TuneStrainState.AddTemporaryResponse(Owner, card);
    }

    protected override void OnUpgrade() { }
}
