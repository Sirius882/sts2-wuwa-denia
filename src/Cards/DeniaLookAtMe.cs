using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

public sealed class DeniaLookAtMe : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Ethereal };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_look_at_me.png";

    public DeniaLookAtMe()
        : base(0, CardType.Power, CardRarity.Basic, TargetType.Self, showInCardLibrary: true) { }

    public override int MaxUpgradeLevel => 0;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature)) return;

        await DeniaFormHelper.MarkLookAtMeSeenThisBlackForm(Owner.Creature, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
        {
            await PowerCmd.Apply<DeniaResonanceModePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
            await DeniaFormHelper.MarkTemporaryResonanceMode(Owner.Creature, Owner.Creature, this);
        }

        // 消耗手牌中的"怜悯我"和所有"直视我"（必须 await，避免多人 desync）
        foreach (var card in Owner.PlayerCombatState.Hand.Cards.ToList())
        {
            if (card is DeniaPityMe || card is DeniaLookAtMe)
                await CardCmd.Exhaust(ctx, card);
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "直视我", Description: "只能在[gold]黑色形态[/gold]下打出。\n消耗手牌中的\"直视我\"和\"怜悯我\"。\n虚质强化：进入[gold]共鸣模态·集谐[/gold]。退出[gold]黑色形态[/gold]时，也退出[gold]共鸣模态·集谐[/gold]。");
}
