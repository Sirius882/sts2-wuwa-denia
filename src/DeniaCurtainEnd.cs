using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
public sealed class DeniaCurtainEnd : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_curtain_end.png";
    public DeniaCurtainEnd() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "帷幕终景", Description: "选择一张手牌，将其复制品放入[gold]抽牌堆[/gold]。复制品获得[gold]消耗[/gold]{IfUpgraded:show:|、[gold]虚无[/gold]}。{IfUpgraded:show:|\n自牌消耗。}");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { var hand = Owner.PlayerCombatState.Hand.Cards.ToList(); if (hand.Count == 0) return; var selector = new MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs(new MegaCrit.Sts2.Core.Localization.LocString("gameplay_ui", "CHOOSE_CARD_UPGRADE_HEADER"), 1); var pick = (await CardSelectCmd.FromHand(ctx, Owner, selector, null, this)).FirstOrDefault(); if (pick == null) return; var dupe = pick.CreateClone(); if (dupe != null) { dupe.AddKeyword(CardKeyword.Exhaust); if (!IsUpgraded) dupe.AddKeyword(CardKeyword.Ethereal); var cb = Owner.Creature.CombatState; await CardPileCmd.AddGeneratedCardToCombat(dupe, PileType.Draw, Owner); } }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
