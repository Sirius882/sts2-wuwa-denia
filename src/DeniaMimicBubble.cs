using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>拟态泡泡 — Rare Skill</summary>
public sealed class DeniaMimicBubble : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_mimic_bubble.png";

    public DeniaMimicBubble()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<IntangiblePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        if (await TrySpendDarkCore(play))
            await PowerCmd.Apply<IntangiblePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "拟态泡泡",
            Description: "获得1层[gold]无实体[/gold]。\n黯核强化：再获得1层[gold]无实体[/gold]。");
}
