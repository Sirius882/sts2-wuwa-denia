using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
public sealed class DeniaConformalEnergy : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_conformal_energy.png";
    public DeniaConformalEnergy() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "共形能量", Description: "抽{IfUpgraded:show:3|2}张牌。\n黯核强化：获得1点[gold]能量[/gold]。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { int n = IsUpgraded ? 3 : 2; if (await TrySpendDarkCore(play)) await PlayerCmd.GainEnergy(1m, Owner); await CardPileCmd.Draw(ctx, n, Owner); }
    protected override void OnUpgrade() { }
}
