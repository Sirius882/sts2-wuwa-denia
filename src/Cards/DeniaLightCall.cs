using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using TuneStrain;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaLightCall : CustomCardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { TuneStrainKeywords.TuneStrainResponse };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_light_call.png";
    public DeniaLightCall() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "轻唤", Description: "每当附加聚爆时，同步附加一半层数的易伤。不可叠加。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { if (Owner.Creature.GetPower<DeniaLightCallPower>() != null) return; await PowerCmd.Apply<DeniaLightCallPower>(ctx, Owner.Creature, 1m, Owner.Creature, this); }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
