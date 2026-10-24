#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>人类的喜好 — Uncommon Skill: apply 2 bias + unconditional break; upgrade = all enemies. Always lose 1 energy.</summary>
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
            Description: "{IfUpgraded:show:给所有敌人|}附加2[color=#9A6A18]集谐·偏移[/color]，无条件[color=#9A6A18]谐度破坏[/color]。失去1能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        if (IsUpgraded)
        {
            var combatState = Owner.Creature.CombatState;
            ArgumentNullException.ThrowIfNull(combatState);
            foreach (var enemy in combatState.HittableEnemies)
            {
                await TuneStrainState.TryAddBias(enemy, 2, Owner.Creature, this);
                if (!enemy.IsDead)
                    await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(enemy, Owner.Creature, this);
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(play.Target);
            await TuneStrainState.TryAddBias(play.Target, 2, Owner.Creature, this);
            if (!play.Target.IsDead)
                await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        }

        await PlayerCmd.LoseEnergy(1m, Owner);
    }

    protected override void OnUpgrade() { }
}
