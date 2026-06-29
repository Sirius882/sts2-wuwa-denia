#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>心锁 — Uncommon Skill, 0e. Exhaust+Retain. 黑→6VM, 粉→1能量。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaHeartLock : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, CardKeyword.Retain, DeniaSpecialKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_heart_lock.png";

    public DeniaHeartLock()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "心锁",
        Description: "若处于[gold]黑色[/gold]形态，获得6[gold]虚质[/gold]；若处于[gold]粉色[/gold]形态，获得1点能量。{IfUpgraded:show:抽1张牌。|}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaResourceState.GainVirtualMatter(Owner.Creature, 6, Owner.Creature, this);
        else
            await PlayerCmd.GainEnergy(1m, Owner);

        if (IsUpgraded)
            await CardPileCmd.Draw(ctx, 1, Owner);
    }

    protected override void OnUpgrade() { }
}
