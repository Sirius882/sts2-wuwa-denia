using System.Collections.Generic;
using System.Threading;
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

    /// <summary>本场战斗累计消耗的虚质总量。</summary>
    public int TotalVMSpent;

    /// <summary>待发放的虚质消耗量累加器。</summary>
    public static int PendingVMSpent;

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "虚质科学直觉",
            Description: "本场战斗每消耗10点虚质，获得1点能量。",
            SmartDescription: "本场战斗每消耗10点虚质，获得1点能量。");

    /// <summary>累加虚质消耗量（在补丁中调用）。</summary>
    public static void AccumulateVM(Creature creature, int amount)
    {
        var power = creature.GetPower<DeniaVirtualScienceIntuitionPower>();
        if (power == null || amount <= 0) return;
        power.TotalVMSpent += amount;
        Interlocked.Add(ref PendingVMSpent, amount);
    }

    /// <summary>在 AfterCardPlayed 安全发放能量。</summary>
    public static async Task FlushEnergyAsync(MegaCrit.Sts2.Core.Entities.Players.Player? player)
    {
        if (player == null) return;
        int pending = Interlocked.Exchange(ref PendingVMSpent, 0);
        if (pending <= 0) return;
        var power = player.Creature?.GetPower<DeniaVirtualScienceIntuitionPower>();
        if (power == null) return;
        int energyGain = power.TotalVMSpent / 10 - (power.TotalVMSpent - pending) / 10;
        if (energyGain > 0)
            await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainEnergy(energyGain, player);
    }
}
