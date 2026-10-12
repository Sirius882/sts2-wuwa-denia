#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using TuneStrain.Powers;

namespace TuneStrain;

/// <summary>
/// 集谐系统的核心状态与对外 API。
/// 写入走 PowerCmd / CardCmd；调用方必须 await 异步 API。
/// 本场力量奖励用隐藏 power；临时响应用卡牌 Keywords 为唯一真源
/// （有响应 keyword 且不在 CanonicalKeywords = 临时）。
/// </summary>
public static class TuneStrainState
{
    private const int InitialInterferenceDuration = 3;
    private const int InitialResponseDuration = 3;
    private const int BreakStrengthPerBreakCap = 2;
    private const int BreakStrengthPerCombatCap = 4;

    private static readonly PlayerChoiceContext Throwing = new ThrowingPlayerChoiceContext();

    // 角色 mod 注册的响应度倍率（进程级、纯计算，不进 checksum）
    private static readonly List<Func<Creature, double>> ResponseDegreeMultipliers = new();

    static TuneStrainState()
    {
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    private static void OnCombatEnded(CombatRoom room)
    {
        try
        {
            ClearTemporaryResponses(room.CombatState);
        }
        catch (Exception ex)
        {
            TuneStrainLog.Info($"ClearTemporaryResponses on CombatEnded failed: {ex.Message}");
        }
    }

    // ---------------- 集谐·偏移 ----------------

    public static int GetBias(Creature target) =>
        target.GetPower<TuneStrainBiasPower>()?.Amount ?? 0;

    public static bool HasInterference(Creature target) =>
        target.GetPower<TuneStrainInterferencePower>() != null;

    public static async Task<bool> TryAddBias(Creature target, int amount, Creature applier, CardModel? source)
    {
        if (amount <= 0 || target.IsDead) return false;
        if (HasInterference(target)) return false;

        await PowerCmd.Apply<TuneStrainBiasPower>(Throwing, target, amount, applier, source);
        return true;
    }

    // ---------------- 集谐响应 power 读取 ----------------

    public static int GetResponsePowerAmount(Creature player) =>
        player.GetPower<TuneStrainResponsePower>()?.Amount ?? 0;

    public static double GetResponseDegree(Creature player)
    {
        int layers = GetResponsePowerAmount(player);
        if (layers <= 0) return 0.0;

        double effective = layers;
        foreach (var multiplier in ResponseDegreeMultipliers)
        {
            try { effective *= multiplier(player); }
            catch { /* 单个 provider 出错不应破坏整体 */ }
        }
        return TuneStrainMath.GetResponseDegree((int)Math.Floor(effective));
    }

    public static IDisposable RegisterResponseDegreeMultiplier(Func<Creature, double> multiplier)
    {
        ArgumentNullException.ThrowIfNull(multiplier);
        ResponseDegreeMultipliers.Add(multiplier);
        return new MultiplierHandle(() => ResponseDegreeMultipliers.Remove(multiplier));
    }

    private sealed class MultiplierHandle : IDisposable
    {
        private Action? _release;
        public MultiplierHandle(Action release) => _release = release;
        public void Dispose() { Interlocked.Exchange(ref _release, null)?.Invoke(); }
    }

    // ---------------- 临时集谐响应（Keywords 为真源） ----------------

    /// <summary>
    /// 是否为「临时」集谐响应：当前 Keywords 含响应，但 CanonicalKeywords 不含（非永久自带）。
    /// </summary>
    public static bool HasTemporaryResponse(CardModel card) =>
        card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
        && !card.CanonicalKeywords.Contains(TuneStrainKeywords.TuneStrainResponse);

    /// <summary>
    /// 给牌组永久牌及其本场战斗副本附加临时集谐响应。
    /// 真源是 Keywords；处决/战斗结束时 ClearTemporaryResponses 清掉。
    /// </summary>
    public static void AddTemporaryResponse(Player player, CardModel card)
    {
        CardModel deckCard = card.DeckVersion ?? card;
        TagTemporary(deckCard);

        var pcs = player.PlayerCombatState;
        if (pcs == null) return;
        foreach (var combatCard in pcs.AllCards)
        {
            if (ReferenceEquals(combatCard, deckCard)) continue;
            if (ReferenceEquals(combatCard.DeckVersion, deckCard))
                TagTemporary(combatCard);
        }
    }

    private static void TagTemporary(CardModel card)
    {
        if (card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)) return;
        CardCmd.ApplyKeyword(card, TuneStrainKeywords.TuneStrainResponse);
    }

    /// <summary>
    /// 清除临时集谐响应（设计：集谐处决时清除所有玩家的临时标记；战斗结束同样清理）。
    /// 永久 CanonicalKeywords 响应不受影响。
    /// </summary>
    public static void ClearTemporaryResponses(ICombatState? combatState = null)
    {
        if (combatState == null) return;
        foreach (var player in combatState.Players)
            ClearPlayerTemporaryResponses(player, combatState);
    }

    private static void ClearPlayerTemporaryResponses(Player player, ICombatState combatState)
    {
        _ = combatState;
        // 战斗内所有牌堆（PlayerCombatState.AllCards）
        var pcs = player.PlayerCombatState;
        if (pcs != null)
        {
            foreach (var card in pcs.AllCards.ToList())
                RemoveTemporaryResponseKeyword(card);
        }

        try
        {
            foreach (var card in player.Deck.Cards.ToList())
                RemoveTemporaryResponseKeyword(card);
        }
        catch
        {
            // Deck 在某些生命周期不可用
        }
    }

    private static void RemoveTemporaryResponseKeyword(CardModel card)
    {
        try
        {
            if (card.HasBeenRemovedFromState) return;
            if (!HasTemporaryResponse(card)) return;
            CardCmd.RemoveKeyword(card, TuneStrainKeywords.TuneStrainResponse);
        }
        catch { }
    }

    // ---------------- 集谐处决 ----------------

    public static async Task ResolveRupture(Creature target, Creature applier, CardModel? source)
    {
        int bias = GetBias(target);
        if (bias <= 0 || target.IsDead) return;

        await PowerCmd.Remove<TuneStrainBiasPower>(target);

        await PowerCmd.Apply<TuneStrainInterferencePower>(Throwing, target, bias, applier, source);
        await PowerCmd.Apply<TuneStrainInterferenceDurationPower>(Throwing, target, InitialInterferenceDuration, applier, source);

        await GrantBreakStrengthReward(applier, bias, source);

        Player? player = applier.Player ?? source?.Owner;
        if (player != null)
            await UpdateResponsePowerFromCards(player, applier, source);

        // 设计：处决清除所有临时响应（全局）
        ClearTemporaryResponses(target.CombatState);
    }

    private static async Task GrantBreakStrengthReward(Creature applier, int bias, CardModel? source)
    {
        if (applier.IsDead || bias <= 0) return;
        int perBreak = Math.Min(bias, BreakStrengthPerBreakCap);
        int alreadyGained = applier.GetPower<TuneStrainBreakStrengthGainedPower>()?.Amount ?? 0;
        int gain = Math.Min(perBreak, BreakStrengthPerCombatCap - alreadyGained);
        if (gain <= 0) return;

        await PowerCmd.Apply<StrengthPower>(Throwing, applier, gain, applier, source);
        await PowerCmd.Apply<TuneStrainBreakStrengthGainedPower>(Throwing, applier, gain, applier, source);
    }

    private static async Task UpdateResponsePowerFromCards(Player player, Creature? applier, CardModel? source)
    {
        int totalResponseCards = CountResponseCards(player);

        int current = GetResponsePowerAmount(player.Creature);
        int newAmount = Math.Max(current, totalResponseCards);

        if (newAmount <= 0)
            return;

        int delta = newAmount - current;
        if (delta > 0)
            await PowerCmd.Apply<TuneStrainResponsePower>(Throwing, player.Creature, delta, applier ?? player.Creature, source);

        await PowerCmd.Remove<TuneStrainResponseDurationPower>(player.Creature);
        await PowerCmd.Apply<TuneStrainResponseDurationPower>(Throwing, player.Creature, InitialResponseDuration, applier ?? player.Creature, source);
    }

    private static int CountResponseCards(Player player)
    {
        if (player.PlayerCombatState == null) return 0;
        int count = 0;
        count += CountInPile(PileType.Draw, player);
        count += CountInPile(PileType.Hand, player);
        count += CountInPile(PileType.Discard, player);
        return count;
    }

    private static int CountInPile(PileType pile, Player player)
    {
        try
        {
            return pile.GetPile(player).Cards.Count(c =>
                c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        }
        catch { return 0; }
    }
}
