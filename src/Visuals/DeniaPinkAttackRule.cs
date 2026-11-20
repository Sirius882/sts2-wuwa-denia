using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>只看这次出牌直接发出的各攻击指令的段数；群攻的每个目标共用一次身体动作。</summary>
internal static class DeniaPinkAttackRule
{
    private sealed class PlayState(Creature actor, CardPlay? play)
    {
        internal readonly Creature Actor = actor;
        internal readonly CardPlay? Play = play;
        internal bool Started;
    }
    private static readonly ConditionalWeakTable<CardModel, PlayState> Pending = new();

    internal static void Begin(CardModel card, Creature actor, bool isDenia, CardPlay? play = null)
    {
        Pending.Remove(card);
        if (isDenia && card.Type == CardType.Attack) Pending.Add(card, new PlayState(actor, play));
    }

    internal static bool TryStart(CardModel? card, Creature? actor, int declaredHits, CardPlay? play = null)
    {
        if (card == null || actor == null || !Pending.TryGetValue(card, out var state)
            || !ReferenceEquals(state.Actor, actor)) return false;
        // 后续指令只有携带同一个 CardPlay 才属于该牌的直接伤害。
        if (state.Started && (play == null || !ReferenceEquals(state.Play, play))) return false;
        state.Started = true;
        // 任意来源攻击牌，只看本指令声明段数；黑态四段以上交给专用四挥击动画。
        return declaredHits >= 1;
    }

    internal static void End(CardModel card) => Pending.Remove(card);
}
