using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>蚀域 — Uncommon Skill</summary>
public sealed class DeniaCorrosionDomain : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_corrosion_domain.png";

    public DeniaCorrosionDomain()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "蚀域", Description: "抽{IfUpgraded:show:3|2}张牌。\n选择1张手牌消耗。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int drawCount = IsUpgraded ? 3 : 2;
        await CardPileCmd.Draw(ctx, drawCount, Owner);

        var hand = Owner.PlayerCombatState.Hand.Cards.ToList();
        if (hand.Count == 0) return;

        var sel = new MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs(
            new MegaCrit.Sts2.Core.Localization.LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 1);
        var pick = (await CardSelectCmd.FromHand(ctx, Owner, sel, null, this)).FirstOrDefault();
        if (pick == null) return;

        await CardPileCmd.Add(pick, PileType.Exhaust, CardPilePosition.Bottom, this);
    }

    protected override void OnUpgrade() { }
}
