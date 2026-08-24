using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>久疏问候 — Common Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaLongTimeNoSee : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_long_time_no_see.png";

    public DeniaLongTimeNoSee() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "久疏问候",
            Description: "附加{IfUpgraded:show:6|4}层[color=#9A6A18]虚弱[/color]。\n黯核强化：若本次进入[color=#9A6A18]黑色形态[/color]后打出的是「直视我」，获得等于所给予的虚弱层数总和的[color=#9A6A18]力量[/color]。若是「怜悯我」，获得等量[color=#9A6A18]蔽星[/color]。若是通过「请您不要···宽恕我」进入[color=#9A6A18]黑色形态[/color]，则都获得。这些buff在切回粉色时清除。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int w = IsUpgraded ? 6 : 4;
        await PowerCmd.Apply<WeakPower>(ctx, play.Target, w, Owner.Creature, this);

        if (!await TrySpendDarkCore(play)) return;

        bool forgive = DeniaFormHelper.SawForgiveMePathThisBlackForm(Owner.Creature);
        bool look = forgive || DeniaFormHelper.SawLookAtMeThisBlackForm(Owner.Creature);
        bool pity = forgive || DeniaFormHelper.SawPityMeThisBlackForm(Owner.Creature);

        if (look)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, w, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusStrengthDebt(Owner.Creature, w, Owner.Creature, this);
        }
        if (pity)
        {
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, w, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusTrajectoryDebt(Owner.Creature, w, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
