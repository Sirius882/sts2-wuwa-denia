#nullable enable
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Denia;

public abstract class DeniaHiddenCounterPower : CustomPowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}

public sealed class DeniaFormSwitchedThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaFormSwitchedThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaFormSwitchedThisTurnPower));
}

public sealed class DeniaBlackFormStrengthDebtPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormStrengthDebtPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormStrengthDebtPower));
}

public sealed class DeniaBlackFormTrajectoryDebtPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormTrajectoryDebtPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormTrajectoryDebtPower));
}

public sealed class DeniaBlackFormShroudedStarDebtPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormShroudedStarDebtPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormShroudedStarDebtPower));
}

public sealed class DeniaWeaknessBonusStrengthDebtPower : DeniaHiddenCounterPower
{
    static DeniaWeaknessBonusStrengthDebtPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaWeaknessBonusStrengthDebtPower));
}

public sealed class DeniaWeaknessBonusTrajectoryDebtPower : DeniaHiddenCounterPower
{
    static DeniaWeaknessBonusTrajectoryDebtPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaWeaknessBonusTrajectoryDebtPower));
}

public sealed class DeniaBlackFormTemporaryResonanceModePower : DeniaHiddenCounterPower
{
    static DeniaBlackFormTemporaryResonanceModePower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormTemporaryResonanceModePower));
}

public sealed class DeniaPermanentResonanceModeSeenPower : DeniaHiddenCounterPower
{
    static DeniaPermanentResonanceModeSeenPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaPermanentResonanceModeSeenPower));
}

public sealed class DeniaBuffOrDebuffAppliedThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaBuffOrDebuffAppliedThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBuffOrDebuffAppliedThisTurnPower));
}

public sealed class DeniaKeepRunningTargetIndexPower : DeniaHiddenCounterPower
{
    static DeniaKeepRunningTargetIndexPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaKeepRunningTargetIndexPower));
}

public sealed class DeniaYouTryItTargetIndexPower : DeniaHiddenCounterPower
{
    static DeniaYouTryItTargetIndexPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaYouTryItTargetIndexPower));
}

public sealed class DeniaEntropyBoostTriggeredThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaEntropyBoostTriggeredThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaEntropyBoostTriggeredThisTurnPower));
}

public sealed class DeniaVirtualScienceIntuitionRemainderPower : DeniaHiddenCounterPower
{
    static DeniaVirtualScienceIntuitionRemainderPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaVirtualScienceIntuitionRemainderPower));
}

public sealed class DeniaMeltingAwayCapBonusPower : DeniaHiddenCounterPower
{
    static DeniaMeltingAwayCapBonusPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaMeltingAwayCapBonusPower));
}

/// <summary>本场战斗已打出卡牌计数器——骗术师/赝作矮星共享用（用作阈值触发）。</summary>
public sealed class DeniaRelicCardPlayedCounterPower : DeniaHiddenCounterPower
{
    static DeniaRelicCardPlayedCounterPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaRelicCardPlayedCounterPower));
}

/// <summary>本次黑色形态期间打出过「直视我」。</summary>
public sealed class DeniaBlackFormLookAtMeSeenPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormLookAtMeSeenPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormLookAtMeSeenPower));
}

/// <summary>本次黑色形态期间打出过「怜悯我」。</summary>
public sealed class DeniaBlackFormPityMeSeenPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormPityMeSeenPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormPityMeSeenPower));
}

/// <summary>本次黑色形态由「请您不要···宽恕我」进入（虚质粒子/久疏问候双收益路径）。</summary>
public sealed class DeniaBlackFormForgiveMePathPower : DeniaHiddenCounterPower
{
    static DeniaBlackFormForgiveMePathPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaBlackFormForgiveMePathPower));
}

/// <summary>幻沫：本回合已触发次数。</summary>
public sealed class DeniaPhantomFoamTriggeredThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaPhantomFoamTriggeredThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaPhantomFoamTriggeredThisTurnPower));
}

/// <summary>向虚而行：本回合已触发次数。</summary>
public sealed class DeniaTowardVoidTriggeredThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaTowardVoidTriggeredThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaTowardVoidTriggeredThisTurnPower));
}

/// <summary>止痛药：本场战斗已发放过开场 5 力/敏/蔽星（每玩家独立）。</summary>
public sealed class DeniaPainkillerOpeningBuffUsedPower : DeniaHiddenCounterPower
{
    static DeniaPainkillerOpeningBuffUsedPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaPainkillerOpeningBuffUsedPower));
}

/// <summary>献斗系列：本场战斗已掉过血（每玩家独立）。</summary>
public sealed class DeniaSacrificialHitThisCombatPower : DeniaHiddenCounterPower
{
    static DeniaSacrificialHitThisCombatPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaSacrificialHitThisCombatPower));
}

/// <summary>大师之剑：本场战斗已做过开战 setup（每玩家独立）。</summary>
public sealed class DeniaMasterSwordSetupDonePower : DeniaHiddenCounterPower
{
    static DeniaMasterSwordSetupDonePower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaMasterSwordSetupDonePower));
}

