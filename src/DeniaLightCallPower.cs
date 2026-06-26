using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>轻唤: 附加聚爆时同步附加一半层数(向上取整)的易伤</summary>
public sealed class DeniaLightCallPower : CustomPowerModel, IOnFusionBurstAppliedPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_light_call_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_light_call_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "轻唤", Description: "每当附加聚爆时，同步附加一半层数的易伤。", SmartDescription: "每当附加聚爆时，同步附加一半层数的易伤。");

    public async Task OnFusionBurstApplied(Creature target, int amount)
    {
        if (Owner.IsDead || amount <= 0) return;
        if (AemeathFusionBurstState.IsBurstProcessing) return;
        int halfAmount = (amount + 1) / 2;
        await PowerCmd.Apply<VulnerablePower>(
            new ThrowingPlayerChoiceContext(), target, halfAmount, Owner, null);
    }
}
