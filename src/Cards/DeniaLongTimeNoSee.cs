using System.Linq;
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

    public DeniaLongTimeNoSee() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "久疏问候",
            Description: "选择：给予目标{IfUpgraded:show:5|3}层虚弱，或给予所有敌人{IfUpgraded:show:3|2}层虚弱。\n黯核强化：若本次进入黑色形态后打出的是“直视我”，获得等于所给予的虚弱层数总和的力量。若是“怜悯我”，获得等量蔽星。若是通过“请您不要···宽恕我”进入黑色形态，则都获得。这些buff在切换回粉色形态时清除。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool all = await DeniaTargetChoice.ChooseAll<DeniaWeakSingleChoice, DeniaWeakAllChoice>(ctx, this);
        int w = all ? (IsUpgraded ? 3 : 2) : (IsUpgraded ? 5 : 3);
        var enemies = all
            ? Owner.Creature.CombatState.Enemies.Where(enemy => !enemy.IsDead).ToArray()
            : new[] { play.Target };
        int totalWeak = 0;
        foreach (var enemy in enemies)
        {
            int before = enemy.GetPower<WeakPower>()?.Amount ?? 0;
            await PowerCmd.Apply<WeakPower>(ctx, enemy, w, Owner.Creature, this);
            totalWeak += System.Math.Max(0, (enemy.GetPower<WeakPower>()?.Amount ?? 0) - before);
        }
        if (!await TrySpendDarkCore(play)) return;
        bool forgive = DeniaFormHelper.SawForgiveMePathThisBlackForm(Owner.Creature);
        bool look = forgive || DeniaFormHelper.SawLookAtMeThisBlackForm(Owner.Creature);
        bool pity = forgive || DeniaFormHelper.SawPityMeThisBlackForm(Owner.Creature);
        if (look && totalWeak > 0)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, totalWeak, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusStrengthDebt(Owner.Creature, totalWeak, Owner.Creature, this);
        }
        if (pity && totalWeak > 0)
        {
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, Owner.Creature, totalWeak, Owner.Creature, this);
            await DeniaFormHelper.AddWeaknessBonusTrajectoryDebt(Owner.Creature, totalWeak, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
