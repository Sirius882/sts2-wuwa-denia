using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>只看牌自身首个攻击指令的段数；群攻的每个目标共用一次身体动作。</summary>
internal static class DeniaPinkAttackRule
{
    private sealed class PlayState(Creature actor)
    {
        internal readonly Creature Actor = actor;
        internal bool Started;
    }
    private static readonly ConditionalWeakTable<CardModel, PlayState> Pending = new();

    internal static void Begin(CardModel card, Creature actor, bool isPinkDenia)
    {
        Pending.Remove(card);
        if (isPinkDenia && card.Type == CardType.Attack) Pending.Add(card, new PlayState(actor));
    }

    internal static bool TryStart(CardModel? card, Creature? actor, int declaredHits)
    {
        if (card == null || actor == null || !Pending.TryGetValue(card, out var state)
            || !ReferenceEquals(state.Actor, actor) || state.Started) return false;
        state.Started = true;
        return declaredHits == 1;
    }

    internal static void End(CardModel card) => Pending.Remove(card);
}
