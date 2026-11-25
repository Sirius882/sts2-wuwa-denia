using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

/// <summary>听话 — Uncommon Skill: draw 2(4), exhaust up to 3 hand cards, cleanse 1 debuff type per exhausted card or gain Artifact for excess.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaObey : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_obey.png";

    public DeniaObey()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "听话",
            Description: "抽{IfUpgraded:show:4|2}张牌，选择最多3张手牌消耗。每消耗1张牌，消除自身1种负面效果（不足时随机消除）；溢出部分每张获得1层[color=#9A6A18]人工制品[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CardPileCmd.Draw(ctx, IsUpgraded ? 4 : 2, Owner);

        const int maxSelect = 3;
        var hand = PileType.Hand.GetPile(Owner);
        if (!hand.Cards.Any()) return;

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "TO_EXHAUST"), 0, maxSelect);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs, c => c != this, this);
        if (selected == null) return;

        int exhausted = 0;
        foreach (var card in selected.ToList())
        {
            await CardCmd.Exhaust(ctx, card);
            exhausted++;
        }
        if (exhausted <= 0) return;

        // 含负层数的允许负值状态（如负聚爆轨迹、负力量等，TypeForCurrentAmount 为 Debuff）。
        var debuffGroups = Owner.Creature.Powers
            .Where(p => p.Amount != 0 && p.TypeForCurrentAmount == PowerType.Debuff)
            .GroupBy(p => p.GetType())
            .Select(g => g.ToList())
            .OrderBy(g => g[0].GetType().Name, StringComparer.Ordinal)
            .ToList();

        int toCleanse = System.Math.Min(exhausted, debuffGroups.Count);
        int overflow = exhausted - debuffGroups.Count;

        if (exhausted < debuffGroups.Count)
        {
            for (int i = 0; i < toCleanse; i++)
            {
                var picked = Owner.RunState is NullRunState
                    ? debuffGroups[0]
                    : Owner.RunState.Rng.CombatCardSelection.NextItem(debuffGroups) ?? debuffGroups[0];
                debuffGroups.Remove(picked);
                foreach (var power in picked)
                    await PowerCmd.Remove(power);
            }
        }
        else
        {
            foreach (var group in debuffGroups)
                foreach (var power in group)
                    await PowerCmd.Remove(power);
        }

        if (overflow > 0)
            await PowerCmd.Apply<ArtifactPower>(ctx, Owner.Creature, overflow, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
