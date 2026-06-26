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
        new PowerLoc(Title: "熵变强化", Description: "每当自己获得增益或给敌人附加减益时，获得2点格挡。", SmartDescription: "每当自己获得增益或给敌人附加减益时，获得2点格挡。");

    // 累加器模式（见经验总结 #63）
    private static readonly System.Collections.Generic.Dictionary<Creature, int> _pendingBlock = new();
    private static readonly object _lock = new();

    public static void AccumulateBlock(Creature creature, int amount)
    {
        if (amount <= 0) return;
        lock (_lock)
            _pendingBlock[creature] = _pendingBlock.GetValueOrDefault(creature) + amount;
    }

    public static async Task FlushBlockAsync(Creature creature)
    {
        int total;
        lock (_lock)
        {
            if (!_pendingBlock.Remove(creature, out total)) return;
        }
        if (total > 0)
            await CreatureCmd.GainBlock(
                creature, new BlockVar(total, ValueProp.Unpowered), null);
    }
}
