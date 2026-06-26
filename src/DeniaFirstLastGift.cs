using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>
/// 最初和最后的礼物 — Ancient Skill. 仅由 Archaic Tooth 从"请您不要···"转化而来。
/// autoAdd:false 不入随机池；showInCardLibrary:true 图鉴可见。
/// 与 AemeathShiningEnstage 同定位，参数一致。
/// </summary>
public sealed class DeniaFirstLastGift : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_first_last_gift.png";

    public DeniaFirstLastGift()
        : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self, autoAdd: true, showInCardLibrary: true) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "最初和最后的礼物",
        Description: "获得12虚质和3黯核。\n若处于[gold]粉色[/gold]形态，切换到[gold]黑色[/gold]形态，获得「直视我」和「怜悯我」。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 无论形态：获得12虚质和3黯核
        await DeniaResourceState.GainVirtualMatter(Owner.Creature, 12, Owner.Creature, this);
        await DeniaResourceState.GainDarkCore(Owner.Creature, 3, Owner.Creature, this);

        // 粉色形态下切换到黑色并获得直视我+怜悯我
        if (DeniaFormHelper.IsPink(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
            var cb = Owner.Creature.CombatState;
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaPityMe>(Owner), PileType.Hand, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(
                cb.CreateCard<DeniaLookAtMe>(Owner), PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
