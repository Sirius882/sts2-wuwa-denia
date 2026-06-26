using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaLightCall : CustomCardModel
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_light_call.png";
    public DeniaLightCall() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "轻唤", Description: "每当附加[gold]聚爆[/gold]时，同步附加一半层数的[gold]易伤[/gold]。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { await PowerCmd.Apply<DeniaLightCallPower>(ctx, Owner.Creature, 1m, Owner.Creature, this); }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
