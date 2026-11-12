#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>蜜桃冰：夏日海滩事件专属技能。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPeachIce : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_peach_ice.jpg";

    public DeniaPeachIce()
        : base(2, CardType.Skill, CardRarity.Event, TargetType.AnyEnemy, showInCardLibrary: true) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "蜜桃冰",
        Description: "给目标附加{IfUpgraded:show:40|20}点[color=#9A6A18]聚爆上限[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(
            play.Target, IsUpgraded ? 40 : 20, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
