using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

/// <summary>飨宴 — Uncommon Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaDetermination : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_determination.png";
    public override bool GainsBlock => true;

    public DeniaDetermination() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "飨宴",
            Description: "获得等于牌组中[color=#9A6A18]集谐响应[/color]标记总量{IfUpgraded:show:+4|}的[color=#9A6A18]格挡[/color]。\n虚质强化：重复效果。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int responseCards = CountResponseCards();
        int bonus = IsUpgraded ? 4 : 0;
        // 基础格挡 = 三堆集谐响应标记总量（非一半）+ 升级 +4
        int val = responseCards + bonus;
        int times = await TrySpendVirtualMatter(play) ? 2 : 1;

        for (int i = 0; i < times; i++)
        {
            if (val > 0)
                await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(val, ValueProp.Move), play);
        }
    }

    private int CountResponseCards()
    {
        int count = 0;
        count += PileType.Hand.GetPile(Owner).Cards.Count(c => c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        count += PileType.Draw.GetPile(Owner).Cards.Count(c => c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        count += PileType.Discard.GetPile(Owner).Cards.Count(c => c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        return count;
    }

    protected override void OnUpgrade() { }
}
