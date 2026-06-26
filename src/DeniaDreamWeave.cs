using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>织梦 — Uncommon Skill</summary>
public sealed class DeniaDreamWeave : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(8m, ValueProp.Move) };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_dream_weave.png";
    public override bool GainsBlock => true;

    public DeniaDreamWeave() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "织梦",
        Description: "获得{Block:diff()}点[gold]格挡[/gold]。\n对所有敌人附加{IfUpgraded:show:5|3}点[gold]聚爆[/gold]，触发1次[gold]熔解[/gold]。\n虚质强化：熔解结算后，对所有敌人附加2点[gold]聚爆[/gold]2次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        int burst = IsUpgraded ? 5 : 3;
        var snapshot = Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray();

        foreach (var e in snapshot)
        {
            await AemeathFusionBurstState.TryAddFusionBurst(e, burst, Owner.Creature, this);
            await AemeathFusionBurstState.ResolveMelt(e, Owner.Creature, this, 1);
        }

        if (await TrySpendVirtualMatter(play))
        {
            for (int i = 0; i < 2; i++)
            {
                foreach (var e in snapshot)
                {
                    if (!e.IsDead)
                        await AemeathFusionBurstState.TryAddFusionBurst(e, 2, Owner.Creature, this);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
