using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>好累，让我歇会…… — Uncommon Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSoTired : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_so_tired.png";

    public DeniaSoTired()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "好累，让我歇会\u2026\u2026",
            Description: "获得9点[gold]格挡[/gold]。若本回合施加过增益或减益，额外获得9点[gold]格挡[/gold]。\n黯核强化：两段格挡均+4。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dcBonus = await TrySpendDarkCore(play) ? 4 : 0;

        int block1 = 9 + dcBonus;
        int block2 = 9 + dcBonus;

        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block1, ValueProp.Move), play);

        if (DeniaBuffTracker.BuffOrDebuffAppliedThisTurn)
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block2, ValueProp.Move), play);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
