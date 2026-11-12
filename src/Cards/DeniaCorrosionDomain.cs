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
        new CardLoc(Title: "蚀域", Description: "抽{IfUpgraded:show:5|3}张牌。选择{IfUpgraded:show:2|1}张手牌消耗。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CardPileCmd.Draw(ctx, IsUpgraded ? 5 : 3, Owner);
        int count = System.Math.Min(IsUpgraded ? 2 : 1, Owner.PlayerCombatState.Hand.Cards.Count);
        if (count <= 0) return;
        var prefs = new MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs(
            new MegaCrit.Sts2.Core.Localization.LocString("card_selection", "TO_EXHAUST"), count);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs, null, this);
        foreach (var card in selected.ToList())
            await CardCmd.Exhaust(ctx, card);
    }

    protected override void OnUpgrade() { }
}
