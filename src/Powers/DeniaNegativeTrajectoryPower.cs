using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

// Aemeath 的轨迹不保留负层数；用独立欠额承载负值，后续正轨迹先偿还欠额。
public sealed class DeniaNegativeTrajectoryPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;
    public override string? CustomPackedIconPath => "res://aemeath-ww/ui/powers/aemeath_trajectory_fusion_burst_power.webp";
    public override string? CustomBigIconPath => CustomPackedIconPath;
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "聚爆轨迹",
        Description: "聚爆引爆伤害修正{Amount}%。获得的聚爆轨迹先抵消此负值。",
        SmartDescription: "聚爆引爆伤害修正{Amount}%。获得的聚爆轨迹先抵消此负值。");

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner != Owner || (power != this && power is not AemeathFusionBurstTrajectoryPower)) return;
        if (power != this && amount <= 0) return;
        var positive = Owner.GetPower<AemeathFusionBurstTrajectoryPower>();
        if (Amount >= 0 || positive == null || positive.Amount <= 0) return;
        int paid = Math.Min(-Amount, positive.Amount);
        await PowerCmd.ModifyAmount(choiceContext, positive, -paid, applier, cardSource);
        await PowerCmd.ModifyAmount(choiceContext, this, paid, applier, cardSource);
    }

    private static readonly IAemeathAutoBurstDamageRule Rule = new NegativeTrajectoryRule();
    public static void Init() => AemeathFusionBurstRules.RegisterAutoBurstDamageRule(Rule);

    private sealed class NegativeTrajectoryRule : IAemeathAutoBurstDamageRule
    {
        public int Priority => 99;
        public int Apply(AemeathAutoBurstContext context, int currentDamage)
        {
            int debt = context.DamageDealer?.GetPower<DeniaNegativeTrajectoryPower>()?.Amount ?? 0;
            return debt < 0 ? (int)Math.Floor(currentDamage * Math.Max(0m, 1m + debt / 100m)) : currentDamage;
        }
    }
}
