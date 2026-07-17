using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

public sealed class DeniaPityMe : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_pity_me.png";

    public DeniaPityMe()
        : base(0, CardType.Power, CardRarity.Basic, TargetType.Self, showInCardLibrary: true) { }

    public override int MaxUpgradeLevel => 0;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature)) return;

        await DeniaFormHelper.MarkPityMeSeenThisBlackForm(Owner.Creature, Owner.Creature, this);

        int starGain = await TrySpendVirtualMatter(play) ? 2 : 0;
        if (starGain > 0)
        {
            await PowerCmd.Apply<DeniaShroudedStarPower>(
                ctx, Owner.Creature, starGain, Owner.Creature, this);
            await DeniaFormHelper.AddBlackFormShroudedStarDebt(Owner.Creature, starGain, Owner.Creature, this);
        }

        // 消耗手牌中的"直视我"和所有"怜悯我"
        foreach (var card in Owner.PlayerCombatState.Hand.Cards.ToList())
        {
            if (card is DeniaLookAtMe || card is DeniaPityMe)
                _ = CardCmd.Exhaust(ctx, card);
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "怜悯我", Description: "只能在[gold]黑色形态[/gold]下打出。\n消耗手牌中的\"直视我\"和\"怜悯我\"。\n虚质强化：获得2层[gold]蔽星[/gold]（退出[gold]黑色形态[/gold]时失去）。");
}
