#nullable enable
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Denia;

public abstract class DeniaHiddenCounterPower : CustomPowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}

public sealed class DeniaFormSwitchedThisTurnPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaBlackFormStrengthDebtPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaBlackFormTrajectoryDebtPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaBlackFormShroudedStarDebtPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaWeaknessBonusStrengthDebtPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaWeaknessBonusTrajectoryDebtPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaBlackFormTemporaryResonanceModePower : DeniaHiddenCounterPower
{
}

public sealed class DeniaPermanentResonanceModeSeenPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaBuffOrDebuffAppliedThisTurnPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaKeepRunningTargetIndexPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaYouTryItTargetIndexPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaEntropyBoostTriggeredThisTurnPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaVirtualScienceIntuitionRemainderPower : DeniaHiddenCounterPower
{
}

public sealed class DeniaMeltingAwayCapBonusPower : DeniaHiddenCounterPower
{
}

/// <summary>本场战斗已打出卡牌计数器——骗术师/赝作矮星共享用（用作阈值触发）。</summary>
public sealed class DeniaRelicCardPlayedCounterPower : DeniaHiddenCounterPower
{
}

/// <summary>本次黑色形态期间打出过「直视我」。</summary>
public sealed class DeniaBlackFormLookAtMeSeenPower : DeniaHiddenCounterPower
{
}

/// <summary>本次黑色形态期间打出过「怜悯我」。</summary>
public sealed class DeniaBlackFormPityMeSeenPower : DeniaHiddenCounterPower
{
}

/// <summary>本次黑色形态由「请您不要···宽恕我」进入（虚质粒子/久疏问候双收益路径）。</summary>
public sealed class DeniaBlackFormForgiveMePathPower : DeniaHiddenCounterPower
{
}

/// <summary>幻沫：本回合已触发次数。</summary>
public sealed class DeniaPhantomFoamTriggeredThisTurnPower : DeniaHiddenCounterPower
{
}

/// <summary>向虚而行：本回合已触发次数。</summary>
public sealed class DeniaTowardVoidTriggeredThisTurnPower : DeniaHiddenCounterPower
{
}

/// <summary>献斗系列：本场战斗已掉过血（每玩家独立）。</summary>
public sealed class DeniaSacrificialHitThisCombatPower : DeniaHiddenCounterPower
{
}

/// <summary>大师之剑：本场战斗已做过开战 setup（每玩家独立）。</summary>
public sealed class DeniaMasterSwordSetupDonePower : DeniaHiddenCounterPower
{
}

/// <summary>得到太阳：尚未触发响应的聚爆层数+1（1–3），始终保留以免修改回调枚举集合。</summary>
public sealed class DeniaGetSunProgressPower : DeniaHiddenCounterPower
{
}
