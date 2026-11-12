#nullable enable
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace TuneStrain.Powers;

/// <summary>
/// 隐藏计数：本场战斗集谐处决已获得的力量奖励总量（上限 4）。
/// 进 checksum（power id + amount），替代进程内 Dictionary。
/// </summary>
public sealed class TuneStrainBreakStrengthGainedPower : CustomPowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

}
