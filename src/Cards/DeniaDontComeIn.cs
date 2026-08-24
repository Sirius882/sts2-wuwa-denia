using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>不要···进来 — Common Skill: switch form then gain 6 VM; upg +1 DC.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaDontComeIn : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_dont_come_in_new.jpg";

    public DeniaDontComeIn()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "不要···进来",
            Description: "切换形态。获得6[color=#9A6A18]虚质[/color]{IfUpgraded:show:1黯核。|}。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 先切换形态，再获得虚质，避免黑变粉时被清空
        if (DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
        else if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        await DeniaResourceState.GainVirtualMatter(Owner.Creature, 6, Owner.Creature, this);

        if (IsUpgraded)
            await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
