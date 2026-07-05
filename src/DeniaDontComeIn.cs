using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>不要···进来 — Common Skill, 1e(upg:0). Gain DC, switch black, extra VM, get cards.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaDontComeIn : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_dont_come_in_new.jpg";

    public DeniaDontComeIn()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "不要···进来",
        Description: "若处于[gold]粉色形态[/gold]，切换到[gold]黑色形态[/gold]，额外获得6[gold]虚质[/gold]，获得「怜悯我」和「直视我」。{IfUpgraded:show:获得1黯核。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsPink(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
            await DeniaResourceState.GainVirtualMatter(Owner.Creature, 6, Owner.Creature, this);

            if (IsUpgraded)
                await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
