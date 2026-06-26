using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

public sealed class DeniaLookAtMe : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_look_at_me.png";

    public DeniaLookAtMe()
        : base(0, CardType.Attack, CardRarity.Basic, TargetType.Self, showInCardLibrary: false) { }

    public override int MaxUpgradeLevel => 0;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature)) return;

        int strGain = await TrySpendVirtualMatter(play) ? 2 : 0;
        if (strGain > 0)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, strGain, Owner.Creature, this);
            DeniaFormHelper.RecordLookAtMeStrength(Owner.Creature, strGain);
            DeniaFormHelper.SetBuffKind(Owner.Creature, DeniaBlackBuffKind.StrengthOnly);
        }

        // 消耗手牌中的"怜悯我"和所有"直视我"
        foreach (var card in Owner.PlayerCombatState.Hand.Cards.ToList())
        {
            if (card is DeniaPityMe || card is DeniaLookAtMe)
                _ = CardCmd.Exhaust(ctx, card);
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "直视我", Description: "只能在[gold]黑色[/gold]形态下打出。\n消耗手牌中的\"直视我\"和\"怜悯我\"。\n虚质强化：获得2点[gold]力量[/gold]（退出黑色形态时失去）。");
}
