using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 虚质科学直觉 Power: 本场战斗每消耗10虚质，获得1能量。
/// 不可叠加(StackType.Single)。虚质消耗在 Patch 17 累加，能量在 AfterCardPlayed 发放。
/// </summary>
public sealed class DeniaVirtualScienceIntuitionPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_virtual_science_intuition_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_virtual_science_intuition_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "虚质科学直觉",
            Description: "本场战斗每消耗10点虚质，获得1点能量。",
            SmartDescription: "本场战斗每消耗10点虚质，获得1点能量。");

    /// <summary>累加虚质消耗量（在补丁中调用）。</summary>
    public static async Task AccumulateVM(Creature creature, int amount)
    {
        var power = creature.GetPower<DeniaVirtualScienceIntuitionPower>();
        if (power == null || amount <= 0) return;
        var remainderPower = creature.GetPower<DeniaVirtualScienceIntuitionRemainderPower>();
        int total = (remainderPower?.Amount ?? 0) + amount;
        int energyGain = total / 10;
        int remainder = total % 10;

        if (remainderPower != null)
            await PowerCmd.Remove<DeniaVirtualScienceIntuitionRemainderPower>(creature);
        if (remainder > 0)
            await PowerCmd.Apply<DeniaVirtualScienceIntuitionRemainderPower>(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), creature, remainder, creature, null!);

        if (energyGain > 0)
            await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainEnergy(energyGain, creature.Player);
    }
}
