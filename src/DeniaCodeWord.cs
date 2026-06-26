#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>暗号 — Common Skill, 0e. 切换形态。升级后抽1张牌。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaCodeWord : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_code_word.png";

    public DeniaCodeWord()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "暗号",
        Description: "切换形态。若切换为黑色形态，获得“直视我”和“怜悯我”。\n{IfUpgraded:show:抽1张牌。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsPink(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
            var cb = Owner.Creature.CombatState;
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaPityMe>(Owner), PileType.Hand, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaLookAtMe>(Owner), PileType.Hand, Owner);
        }
        else
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
        }

        if (IsUpgraded)
            await CardPileCmd.Draw(ctx, 1, Owner);
    }

    protected override void OnUpgrade() { }
}
