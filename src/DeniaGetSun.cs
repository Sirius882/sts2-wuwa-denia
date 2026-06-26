using System;
using System.Collections.Generic;
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

/// <summary>得到太阳 — Uncommon Attack</summary>
public sealed class DeniaGetSun : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_get_sun.png";

    public DeniaGetSun() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "得到太阳",
        Description: "提高{IfUpgraded:show:5|2}聚爆上限，附加{IfUpgraded:show:9|6}点[gold]聚爆[/gold]。\n虚质强化：若触发[gold]引爆[/gold]，获得6点[gold]格挡[/gold]。若没有触发引爆，不消耗虚质。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int burst = IsUpgraded ? 9 : 6;
        int capInc = IsUpgraded ? 5 : 2;

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, capInc, Owner.Creature, this);

        bool vmSpent = await TrySpendVirtualMatter(play);

        int beforeBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
        int afterBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
        bool burstTriggered = afterBurst < beforeBurst + burst;

        if (vmSpent)
        {
            if (burstTriggered)
            {
                await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(6m, ValueProp.Move), play);
            }
            else
            {
                // Refund VM if burst didn't trigger
                await DeniaResourceState.GainVirtualMatter(Owner.Creature, 2, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade() { }
}
