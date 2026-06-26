using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
public sealed class DeniaFromFar : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_from_far.png";
    public DeniaFromFar() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "从远方", Description: "所有附加[gold]聚爆[/gold]上限效果额外附加1层。\n黯核强化：额外层数变为2。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { int amount = await TrySpendDarkCore(play) ? 2 : 1; await PowerCmd.Apply<DeniaExtraBurstCapPower>(ctx, Owner.Creature, amount, Owner.Creature, this); }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
