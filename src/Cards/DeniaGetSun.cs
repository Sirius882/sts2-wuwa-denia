using System;
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
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGetSun : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_get_sun.png";

    public DeniaGetSun() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "得到太阳",
        Description: "提高{IfUpgraded:show:4|2}聚爆上限，附加{IfUpgraded:show:6|3}点[gold]聚爆[/gold]。\n虚质强化：若这张牌触发[gold]聚爆上限引爆[/gold]，获得6点[gold]格挡[/gold]。若没有触发引爆，不触发虚质强化。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int burst = IsUpgraded ? 6 : 3;
        int capInc = IsUpgraded ? 4 : 2;

        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, capInc, Owner.Creature, this);

        int beforeBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
        int cap = AemeathFusionBurstState.GetFusionBurstCap(play.Target);
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
        int afterBurst = AemeathFusionBurstState.GetFusionBurst(play.Target);
        bool burstTriggered = beforeBurst + burst >= cap && afterBurst < beforeBurst + burst;

        // 仅在实际引爆时才触发虚质强化（消耗虚质并获得格挡）
        if (burstTriggered && await TrySpendVirtualMatter(play))
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(6m, ValueProp.Move), play);
    }

    protected override void OnUpgrade() { }
}
