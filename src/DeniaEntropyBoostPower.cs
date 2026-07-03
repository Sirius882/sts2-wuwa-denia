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

/// <summary>熵变强化: 获得buff/debuff时获得格挡（按instance次数算，非层数）</summary>
public sealed class DeniaEntropyBoostPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";

    /// <summary>黯核强化带来的额外格挡量（0 或 1）。</summary>
    public int ExtraBlock { get; set; }

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "熵变强化", Description: "每当自己获得增益或给敌人附加减益时，获得{Amount}点格挡。", SmartDescription: "每当自己获得增益或给敌人附加减益时，获得{Amount}点格挡。");

    public static void AccumulateBlock(Creature creature, int amount)
    {
        if (amount <= 0) return;
        _ = PowerCmd.Apply<DeniaEntropyBoostPendingBlockPower>(
            new ThrowingPlayerChoiceContext(), creature, amount, creature, null!);
    }

    public static async Task FlushBlockAsync(Creature creature)
    {
        var pending = creature.GetPower<DeniaEntropyBoostPendingBlockPower>();
        if (pending == null) return;
        int total = pending.Amount;
        await PowerCmd.Remove<DeniaEntropyBoostPendingBlockPower>(creature);
        if (total > 0)
            await CreatureCmd.GainBlock(
                creature, new BlockVar(total, ValueProp.Unpowered), null);
    }
}
