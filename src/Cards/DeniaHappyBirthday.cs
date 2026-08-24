using System;using System.Collections.Generic;using System.Threading.Tasks;using AemeathWw.Scripts;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaHappyBirthday : DeniaCard
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_happy_birthday.png";
    public DeniaHappyBirthday() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override List<(string, string)>? Localization => new CardLoc(Title: "生日快乐", Description: "所有我方玩家获得{IfUpgraded:show:5|3}点[color=#9A6A18]蔽星[/color]。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int amount = IsUpgraded ? 5 : 3;
        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
            return;
        }

        foreach (var player in combatState.Players)
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, player.Creature, amount, Owner.Creature, this);
    }
    protected override void OnUpgrade() { }
}
