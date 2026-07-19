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

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaHumanPreference : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_human_preference.png";

    public DeniaHumanPreference()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "人类的喜好",
        Description: "附加2[gold]集谐·偏移[/gold]。\n黯核强化：给其他所有敌人也附加2[gold]集谐·偏移[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = ctx;
        ArgumentNullException.ThrowIfNull(play.Target);
        bool hitAllEnemies = await TrySpendDarkCore(play);
        if (hitAllEnemies)
        {
            var combatState = Owner.Creature.CombatState;
            ArgumentNullException.ThrowIfNull(combatState);
            foreach (var enemy in combatState.HittableEnemies)
                await TuneStrainState.TryAddBias(enemy, 2, Owner.Creature, this);
        }
        else
        {
            await TuneStrainState.TryAddBias(play.Target, 2, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
