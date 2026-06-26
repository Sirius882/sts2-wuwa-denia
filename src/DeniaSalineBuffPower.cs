using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>生理盐水临时buff——回合结束时移除对应力敏</summary>
public sealed class DeniaSalineBuffPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        if (Amount <= 0) return;
        var owner = Owner;
        if (owner == null) return;

        await RemoveTempBuff(owner, ModelDb.Power<StrengthPower>(), 5m);
        await RemoveTempBuff(owner, ModelDb.Power<DexterityPower>(), 5m);

        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -(decimal)Amount, owner, null!);
    }

    private static async Task RemoveTempBuff(Creature owner, PowerModel canonical, decimal amount)
    {
        var power = owner.GetPower(canonical.Id);
        if (power != null && power.Amount >= amount)
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), power, -amount, owner, null!);
    }
}
