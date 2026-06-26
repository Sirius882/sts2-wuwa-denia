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

/// <summary>久疏问候 — Common Skill</summary>
public sealed class DeniaLongTimeNoSee : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_long_time_no_see.png";

    public DeniaLongTimeNoSee() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "久疏问候",
        Description: "给予目标{IfUpgraded:show:6|4}层[gold]虚弱[/gold]。\n黯核强化：若进入黑色形态后打出「直视我」，获得等量[gold]力量[/gold]；若打出「怜悯我」，获得10倍[gold]聚爆轨迹[/gold]。切换粉色时清除。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int w = IsUpgraded ? 6 : 4;
        await PowerCmd.Apply<WeakPower>(ctx, play.Target, w, Owner.Creature, this);

        if (await TrySpendDarkCore(play))
        {
            var kind = DeniaFormHelper.GetBuffKind(Owner.Creature);
            if (kind == DeniaBlackBuffKind.StrengthOnly || kind == DeniaBlackBuffKind.Both)
            {
                await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, w, Owner.Creature, this);
                DeniaFormHelper.RecordStrength(Owner.Creature, w);
            }
            if (kind == DeniaBlackBuffKind.TrajectoryOnly || kind == DeniaBlackBuffKind.Both)
            {
                int traj = w * 10;
                await PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(
                    ctx, Owner.Creature, traj, Owner.Creature, this);
                DeniaFormHelper.RecordTrajectory(Owner.Creature, traj);
            }
        }
    }

    protected override void OnUpgrade() { }
}
