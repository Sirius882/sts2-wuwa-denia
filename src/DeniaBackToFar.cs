using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
public sealed class DeniaBackToFar : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_back_to_far.png";
    public DeniaBackToFar() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "回到远方", Description: "所有附加[gold]聚爆[/gold]的效果额外附加2层。\n黯核强化：额外层数变为3。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { int amount = await TrySpendDarkCore(play) ? 3 : 2; await PowerCmd.Apply<DeniaExtraBurstPower>(ctx, Owner.Creature, amount, Owner.Creature, this); }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
