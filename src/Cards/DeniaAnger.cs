using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.CardSelection;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
/// <summary>怒 — Rare Skill, 1e. 消耗手牌获得格挡。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaAnger : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Retain };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_anger.png";
    public DeniaAnger() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "怒", Description: "从当前牌组中选择最多{IfUpgraded:show:3|2}张牌消耗。每消耗1张牌，获得{IfUpgraded:show:8|6}格挡。\n虚质强化：每段格挡+2。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int maxSelect = IsUpgraded ? 3 : 2;
        int blockPer = IsUpgraded ? 8 : 6;
        if (await TrySpendVirtualMatter(play)) blockPer += 2;
        var cards = new[] { PileType.Hand, PileType.Draw, PileType.Discard }
            .SelectMany(pile => pile.GetPile(Owner).Cards).Where(card => card != this).ToList();
        if (cards.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "TO_EXHAUST"), 0, maxSelect);
        var selected = await CardSelectCmd.FromSimpleGrid(ctx, cards, Owner, prefs);
        foreach (var card in selected.ToList())
        {
            await CardCmd.Exhaust(ctx, card);
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(blockPer, ValueProp.Move), play);
        }
    }
    protected override void OnUpgrade() { }
}
