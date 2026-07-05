#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System.Linq;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFatalError : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fatal_error.png";

    public DeniaFatalError()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "检验报告RA2499-G",
        Description: "切换形态。对目标附加{IfUpgraded:show:2|1}点[gold]集谐·偏移[/gold]。\n黯核强化：附加的[gold]集谐·偏移[/gold]扩展到所有敌人。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool hitAllEnemies = await TrySpendDarkCore(play);

        if (DeniaFormHelper.IsPink(Owner.Creature))
            await DeniaFormHelper.SwitchToBlack(Owner.Creature, Owner.Creature, this);
        else
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        int bias = IsUpgraded ? 2 : 1;
        if (hitAllEnemies)
        {
            var combatState = Owner.Creature.CombatState;
            ArgumentNullException.ThrowIfNull(combatState);
            foreach (var enemy in combatState.HittableEnemies.ToList())
                await TuneStrainState.TryAddBias(enemy, bias, Owner.Creature, this);
        }
        else
        {
            await TuneStrainState.TryAddBias(play.Target, bias, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}