#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>人类的喜好 — Uncommon Skill: apply 2 bias; upgrade = all enemies.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaHumanPreference : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_human_preference.png";

    public DeniaHumanPreference()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "人类的喜好",
            Description: "{IfUpgraded:show:给所有敌人|}附加2[color=#9A6A18]集谐·偏移[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        if (IsUpgraded)
        {
            var combatState = Owner.Creature.CombatState;
            ArgumentNullException.ThrowIfNull(combatState);
            foreach (var enemy in combatState.HittableEnemies)
                await TuneStrainState.TryAddBias(enemy, 2, Owner.Creature, this);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(play.Target);
            await TuneStrainState.TryAddBias(play.Target, 2, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
