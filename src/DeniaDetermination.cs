using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>飨宴 — Uncommon Attack</summary>
public sealed class DeniaDetermination : DeniaCard
{
    public override int CurrentVirtualMatterCost => 6;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_determination.png";
    public override bool GainsBlock => true;

    public DeniaDetermination() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "飨宴",
        Description: "获得等于当前[gold]力量[/gold]的[gold]格挡[/gold]。对敌方全体造成等于当前[gold]力量[/gold]的伤害。{IfUpgraded:show:\n数值+4|}\n虚质强化：重复一次主效果。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int str = (int)(Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0m);
        int bonus = IsUpgraded ? 4 : 0;
        int val = str + bonus;
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

    protected override void OnUpgrade() { }
}
