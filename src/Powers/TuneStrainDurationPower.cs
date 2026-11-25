#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐独立计时实例基类：每次附加独立存在，各自倒数剩余回合数；
/// 各自记录该次附加的层数（Stacks），过期移除后同步可见 power 为剩余存活实例中的最大值。
/// </summary>
public abstract class TuneStrainDurationPower : CustomPowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override bool IsVisibleInternal => false;

    protected abstract CombatSide TickSide { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DynamicVar("Stacks", 0m) };

    public int Stacks
    {
        get => (int)DynamicVars["Stacks"].BaseValue;
        set
        {
            AssertMutable();
            DynamicVars["Stacks"].BaseValue = value;
        }
    }

    protected abstract Task OnExpiredAsync();

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != TickSide || Owner.IsDead) return;

        int remaining = Amount - 1;
        if (remaining > 0)
        {
            // 用 ModifyAmount 改 Amount 而非直接写字段，确保走游戏同步通道。
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null);
            return;
        }

        await PowerCmd.Remove(this);
        await OnExpiredAsync();
    }
}
