using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaUntilNextTime : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_until_next_time.png";

    public DeniaUntilNextTime() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "直到下次再见",
        Description: "切换形态，抽2张牌。若为黑变粉，不清空[gold]虚质[/gold]；若为粉变黑，额外获得10[gold]虚质[/gold]。获得1[gold]黯核[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this, clearVM: false);
        }
        else
        {
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
            // 粉色切黑默认+10虚质，再额外+10 → 共额外10；设计：额外获得10虚质（在默认10之外）
            await DeniaResourceState.GainVirtualMatter(Owner.Creature, 10, Owner.Creature, this);
        }

        await CardPileCmd.Draw(ctx, 2, Owner);
        // 获得1黯核，无论形态
        await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
