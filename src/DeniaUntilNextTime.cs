using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaUntilNextTime : CustomCardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_until_next_time.png";
    public DeniaUntilNextTime() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "直到下次再见", Description: "只在[gold]黑色[/gold]形态下有效。\n切换到[gold]粉色[/gold]形态，但[gold]虚质[/gold]不归零。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature)) return;
        await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this, clearVM: false);
    }
    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
