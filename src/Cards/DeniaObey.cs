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
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>听话 — Rare Skill: draw 2, exhaust up to 3/4 hand cards, gain 1 Artifact each.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaObey : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_obey.png";

    public DeniaObey()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "听话",
            Description: "抽2张牌，选择最多{IfUpgraded:show:4|3}张手牌消耗。每消耗1张牌，给自己1层[color=#9A6A18]人工制品[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CardPileCmd.Draw(ctx, 2, Owner);

        int maxSelect = IsUpgraded ? 4 : 3;
        var hand = PileType.Hand.GetPile(Owner);
        if (!hand.Cards.Any()) return;

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "TO_EXHAUST"), 0, maxSelect);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs, c => c != this, this);
        if (selected == null || !selected.Any()) return;

        int count = 0;
        foreach (var card in selected.ToList())
        {
            await CardCmd.Exhaust(ctx, card);
            count++;
        }

        if (count > 0)
            await PowerCmd.Apply<ArtifactPower>(ctx, Owner.Creature, count, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
