using System;
using System.Collections.Generic;
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
public sealed class DeniaVirtualParticle : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_virtual_particle.png";

    public DeniaVirtualParticle() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "虚质粒子",
        Description: "给予所有敌人{IfUpgraded:show:3|2}层[gold]虚弱[/gold]。\n黯核强化：若进入黑色形态后打出「直视我」，获得等量[gold]力量[/gold]；若打出「怜悯我」，获得10倍[gold]聚爆轨迹[/gold]。切换粉色时清除。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int w = IsUpgraded ? 3 : 2;
        var enemies = Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray();

        foreach (var e in enemies)
            await PowerCmd.Apply<WeakPower>(ctx, e, w, Owner.Creature, this);

        if (await TrySpendDarkCore(play))
        {
            int totalWeak = w * enemies.Length;
            var kind = DeniaFormHelper.GetBuffKind(Owner.Creature);
            if (kind == DeniaBlackBuffKind.StrengthOnly || kind == DeniaBlackBuffKind.Both)
            {
                await PowerCmd.Apply<StrengthPower>(ctx, Owner.Creature, totalWeak, Owner.Creature, this);
                DeniaFormHelper.RecordStrength(Owner.Creature, totalWeak);
            }
            if (kind == DeniaBlackBuffKind.TrajectoryOnly || kind == DeniaBlackBuffKind.Both)
            {
                int traj = totalWeak * 10;
                await PowerCmd.Apply<AemeathWw.Scripts.AemeathFusionBurstTrajectoryPower>(
                    ctx, Owner.Creature, traj, Owner.Creature, this);
                DeniaFormHelper.RecordTrajectory(Owner.Creature, traj);
            }
        }
    }

    protected override void OnUpgrade() { }
}
