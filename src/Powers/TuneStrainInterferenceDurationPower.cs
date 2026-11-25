#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;

namespace TuneStrain.Powers;

/// <summary>
/// 隐藏 power：计量单次集谐·干涉实例的剩余持续回合数（初始 3 回合，敌方回合结束扣 1）。
/// 多次附加时各自独立存在并计时，由 <see cref="TuneStrainState"/> 维护可见层数为存活实例最大值。
/// </summary>
public sealed class TuneStrainInterferenceDurationPower : TuneStrainDurationPower
{
    // 采用「敌方回合结束扣 1」以与 Aemeath 干涉保持一致的节奏。
    protected override CombatSide TickSide => CombatSide.Enemy;

    protected override Task OnExpiredAsync() =>
        TuneStrainState.SyncInterferenceVisiblePower(Owner);
}
