using System;using System.Collections.Generic;using System.Threading.Tasks;using AemeathWw.Scripts;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaHappyBirthday : CustomCardModel
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_happy_birthday.png";
    public DeniaHappyBirthday() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "生日快乐", Description: "获得{IfUpgraded:show:50|30}层[gold]聚爆轨迹[/gold]。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int amount = IsUpgraded ? 50 : 30;
        await PowerCmd.Apply<AemeathFusionBurstTrajectoryPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
    }
    protected override void OnUpgrade() { }
}
