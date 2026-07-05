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

public sealed class DeniaEntropyBoostPendingBlockPower : DeniaHiddenCounterPower
{
    static DeniaEntropyBoostPendingBlockPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaEntropyBoostPendingBlockPower));
}

public sealed class DeniaEntropyBoostTriggeredThisTurnPower : DeniaHiddenCounterPower
{
    static DeniaEntropyBoostTriggeredThisTurnPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaEntropyBoostTriggeredThisTurnPower));
}

public sealed class DeniaTorchPineNutPendingStrengthPower : DeniaHiddenCounterPower
{
    static DeniaTorchPineNutPendingStrengthPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaTorchPineNutPendingStrengthPower));
}

public sealed class DeniaVirtualScienceIntuitionRemainderPower : DeniaHiddenCounterPower
{
    static DeniaVirtualScienceIntuitionRemainderPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaVirtualScienceIntuitionRemainderPower));
}

public sealed class DeniaMeltingAwayCapBonusPower : DeniaHiddenCounterPower
{
    static DeniaMeltingAwayCapBonusPower() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaMeltingAwayCapBonusPower));
}

