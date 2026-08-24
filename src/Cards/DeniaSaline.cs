using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSaline : CustomCardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_saline.png";

    public DeniaSaline()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "生理盐水",
            Description: "切换形态。给自己附加1层[color=#9A6A18]易伤[/color]。获得{IfUpgraded:show:3|1}点[color=#9A6A18]力量[/color]、{IfUpgraded:show:3|1}点[color=#9A6A18]敏捷[/color]和{IfUpgraded:show:3|1}[color=#9A6A18]蔽星[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
        else
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);

        await PowerCmd.Apply<VulnerablePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        int amount = IsUpgraded ? 3 : 1;
        await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
        await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
