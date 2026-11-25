#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;

namespace TuneStrain.Powers;

/// <summary>
/// 隐藏 power：计量单次集谐响应实例的剩余持续回合数（初始 3 回合，玩家回合结束扣 1）。
/// 多次触发时各自独立存在并计时，由 <see cref="TuneStrainState"/> 维护可见层数为存活实例最大值。
/// </summary>
public sealed class TuneStrainResponseDurationPower : TuneStrainDurationPower
{
    protected override CombatSide TickSide => CombatSide.Player;

    protected override Task OnExpiredAsync() =>
        TuneStrainState.SyncResponseVisiblePower(Owner);
}
