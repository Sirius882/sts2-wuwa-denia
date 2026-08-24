#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Denia;

/// <summary>虚无的频率 — 每回合随机免费打出图鉴卡；第一回合由阿列夫一接管。</summary>
[Pool(typeof(DeniaRelicPool))]
public sealed class DeniaFrequencyOfNothingness : CustomRelicModel
{
    private const int MaxPossessedCards = 13;

    public override RelicRarity Rarity => RelicRarity.Event;
    protected override string IconBaseName => "denia_frequency_of_nothingness";

    public override List<(string, string)>? Localization => new RelicLoc(
        Title: "虚无的频率",
        Description: "每个回合开始时，随机免费打出一张达妮娅图鉴中的牌。阿列夫一将会接管你的第一回合。",
        Flavor: "从虚无深处传来的鸣式，替你作出了选择。");

    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || player.Creature.IsDead) return;

        var cards = ModelDb.CardPool<DeniaCardPool>().AllCards
            .Where(CanGenerateFromLibrary)
            .ToList();
        if (cards.Count == 0) return;

        CardModel? canonical = player.RunState.Rng.CombatCardSelection.NextItem(cards);
        ICombatState? combatState = player.Creature.CombatState;
        if (canonical == null || combatState == null) return;

        CardModel generated = combatState.CreateCard(canonical, player);
        Flash();
        await CardCmd.AutoPlay(choiceContext, generated, null);
    }

    public override async Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || player.PlayerCombatState.TurnNumber > 1) return;

        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null) return;

        Flash();
        using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
        {
            int cardsPlayed = 0;
            int startTurn = player.PlayerCombatState.TurnNumber;
            for (; cardsPlayed < MaxPossessedCards; cardsPlayed++)
            {
                if (CombatManager.Instance.IsOverOrEnding) break;
                if (CombatManager.Instance.IsPlayerReadyToEndTurn(player)) break;
                if (player.PlayerCombatState.TurnNumber != startTurn) break;

                CardModel? card = PileType.Hand.GetPile(player).Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;

                Creature? target = GetVakuuTarget(card, combatState, player);
                await card.SpendResources();
                await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
            }
        }
    }

    private static bool CanGenerateFromLibrary(CardModel card) =>
        card is DeniaCard
        && card.ShouldShowInCardLibrary
        && card.CanBeGeneratedInCombat
        && !card.Keywords.Contains(CardKeyword.Unplayable);

    private static Creature? GetVakuuTarget(CardModel card, ICombatState combatState, Player player) => card.TargetType switch
    {
        TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
        TargetType.AnyAlly => player.RunState.Rng.CombatTargets.NextItem(
            combatState.Allies.Where(c => c.IsAlive && c.IsPlayer && c != player.Creature)),
        TargetType.AnyPlayer => player.Creature,
        _ => null,
    };
}