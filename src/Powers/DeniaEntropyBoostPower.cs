using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 熵变强化: 获得 buff/debuff 时立刻获得格挡（按 instance 次数，非层数）。
/// 通过 Power 生命周期 AfterPowerAmountChanged 实时结算（引擎 await 该 Hook）。
/// </summary>
public sealed class DeniaEntropyBoostPower : CustomPowerModel
{
    private const int MaxTriggersPerTurn = 6;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "熵变强化",
        Description: "获得增益或给敌人附加减益时，获得{Amount}格挡。每回合最多触发6次。",
        SmartDescription: "获得增益或给敌人附加减益时，获得{Amount}格挡。每回合最多触发6次。");

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0 || Owner == null || Owner.IsDead) return;
        if (Amount <= 0) return;

        // 隐藏计数不触发
        if (power is DeniaEntropyBoostTriggeredThisTurnPower) return;
        if (power.Type == PowerType.None) return;

        // 自己获得增益
        bool isSelfBuff = power.Owner == Owner && power.Type == PowerType.Buff;
        // 给敌人附加减益：目标非玩家，且由自己施加（applier 为自己，或 card 归属自己）
        bool isEnemyDebuff = power.Owner != null
            && !power.Owner.IsPlayer
            && (power.Type == PowerType.Debuff
                || power.GetType().Name.Contains("FusionBurstCap", System.StringComparison.Ordinal));
        if (isEnemyDebuff)
        {
            bool byUs = applier == Owner
                || cardSource?.Owner?.Creature == Owner;
            if (!byUs) return;
        }

        if (!isSelfBuff && !isEnemyDebuff) return;

        int triggered = Owner.GetPower<DeniaEntropyBoostTriggeredThisTurnPower>()?.Amount ?? 0;
        if (triggered >= MaxTriggersPerTurn) return;

        // 先记次数（进 checksum），再立刻发格挡
        await PowerCmd.Apply<DeniaEntropyBoostTriggeredThisTurnPower>(
            choiceContext, Owner, 1m, Owner, cardSource);

        int block = (int)Amount;
        // Move：受敏捷等格挡加成（不要用 Unpowered）
        if (block > 0)
            await CreatureCmd.GainBlock(Owner, new BlockVar(block, ValueProp.Move), null);
    }

    public static async Task ClearTriggerCountAsync(Creature creature)
    {
        if (creature == null) return;
        await PowerCmd.Remove<DeniaEntropyBoostTriggeredThisTurnPower>(creature);
    }
}
