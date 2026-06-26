using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.CardSelection;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
/// <summary>怒 — Uncommon Skill, 1e. 消耗手牌获得格挡。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaAnger : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Retain };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_anger.png";
    public DeniaAnger() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "怒", Description: "选择最多{IfUpgraded:show:4|3}张手牌消耗。每消耗1张牌，获得{IfUpgraded:show:4|3}格挡。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int maxSelect = IsUpgraded ? 4 : 3;
        int blockPer = IsUpgraded ? 4 : 3;
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
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(count * blockPer, ValueProp.Unpowered), play);
    }
    protected override void OnUpgrade() { }
}
