using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

/// <summary>飨宴 — Uncommon Attack</summary>
public sealed class DeniaDetermination : DeniaCard
{
    public override int CurrentVirtualMatterCost => 6;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_determination.png";
    public override bool GainsBlock => true;

    public DeniaDetermination() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "飨宴",
        Description: "获得等于当前手牌、抽牌堆和弃牌堆中[gold]集谐响应[/gold]标记总量一半的[gold]格挡[/gold]，对敌方全体造成等量的伤害。{IfUpgraded:show:\n获得的格挡和造成的伤害的基础值+4。|}\n虚质强化：重复一次主效果。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int responseCards = CountResponseCards();
        int bonus = IsUpgraded ? 4 : 0;
        int val = responseCards / 2 + bonus;
        int times = await TrySpendVirtualMatter(play) ? 2 : 1;

        for (int i = 0; i < times; i++)
        {
            if (val > 0)
            {
                await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(val, ValueProp.Move), play);
                await DamageCmd.Attack(val)
                    .FromCard(this)
                    .TargetingAllOpponents(Owner.Creature.CombatState)
                    .WithHitFx("vfx/vfx_heavy_blunt")
                    .Execute(ctx);
            }
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
