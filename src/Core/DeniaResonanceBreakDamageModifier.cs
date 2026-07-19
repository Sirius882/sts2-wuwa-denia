#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

internal static class DeniaResonanceBreakDamageModifier
{
    private sealed class Scope(decimal multiplier, Scope? previous)
    {
        public decimal Multiplier { get; } = multiplier;
        public Scope? Previous { get; } = previous;
        public bool Consumed { get; set; }
    }

    private static readonly AsyncLocal<Scope?> Current = new();
    private static bool _registered;

    public static async Task RunOnce(decimal multiplier, Func<Task> action)
    {
        EnsureRegistered();
        Scope scope = new(multiplier, Current.Value);
        Current.Value = scope;
        try
        {
            await action();
        }
        finally
        {
            Current.Value = scope.Previous;
        }
    }

    private static void EnsureRegistered()
    {
        if (_registered) return;
        AemeathMechanicsApi.RegisterResonanceBreakDamagePercentModifier(ModifyPercent);
        _registered = true;
    }

    private static decimal ModifyPercent(
        Creature target,
        Creature applier,
        CardModel source,
        bool isUnconditional,
        decimal currentPercent)
    {
        _ = target;
        _ = applier;
        _ = source;
        _ = isUnconditional;

        Scope? scope = Current.Value;
        if (scope == null || scope.Consumed)
            return currentPercent;

        scope.Consumed = true;
        return currentPercent * scope.Multiplier;
    }
}