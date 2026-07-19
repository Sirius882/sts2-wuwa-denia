using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>虚质粒子 — Uncommon Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaVirtualParticle : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_virtual_particle.png";

    public DeniaVirtualParticle() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "虚质粒子",
        Description: "给予所有敌人{IfUpgraded:show:3|2}层[gold]虚弱[/gold]。\n黯核强化：若本次进入[gold]黑色形态[/gold]后打出的是「直视我」，获得等于所给予的虚弱层数总和的[gold]力量[/gold]。若是「怜悯我」，获得等量[gold]蔽星[/gold]。若是通过「请您不要···宽恕我」进入[gold]黑色形态[/gold]，则都获得。这些buff在切换粉色时清除。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int w = IsUpgraded ? 3 : 2;
        var enemies = Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray();

        foreach (var e in enemies)
            await PowerCmd.Apply<WeakPower>(ctx, e, w, Owner.Creature, this);

        if (!await TrySpendDarkCore(play)) return;

        int totalWeak = w * enemies.Length;
        bool forgive = DeniaFormHelper.SawForgiveMePathThisBlackForm(Owner.Creature);
        bool look = forgive || DeniaFormHelper.SawLookAtMeThisBlackForm(Owner.Creature);
        bool pity = forgive || DeniaFormHelper.SawPityMeThisBlackForm(Owner.Creature);

        if (look)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, totalWeak, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusStrengthDebt(Owner.Creature, totalWeak, Owner.Creature, this);
        }
        if (pity)
        {
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, totalWeak, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusTrajectoryDebt(Owner.Creature, totalWeak, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
