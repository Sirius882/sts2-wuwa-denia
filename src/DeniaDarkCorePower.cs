using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>黯核: 真值为原生 Stars（PlayerCombatState.Stars），此 Power 仅兼容兜底。
/// 粉色形态回合开始+1，上限3个。标准能力行隐藏，由 NStarCounter 自定义图标显示。</summary>
public sealed class DeniaDarkCorePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}
