using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>破裂 — Rare Attack, AoE</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaRupture : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_rupture.png";

    public DeniaRupture()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "破裂",
            Description: "对全体敌人造成10点伤害{IfUpgraded:show:4|3}次。\n额外造成一次等于全体敌人聚爆上限之和一半的伤害。\n黯核强化：每段基础伤害+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dcBonus = await TrySpendDarkCore(play) ? 5 : 0;
        int hitCount = IsUpgraded ? 4 : 3;
        int baseDmg = 10 + dcBonus;

        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();

        // 多段全屏伤害（用 WithHitCount 确保活力正确加成每段）
        await DamageCmd.Attack(baseDmg)
            .WithHitCount(hitCount)
            .FromCard(this)
            .TargetingAllOpponents(Owner.Creature.CombatState)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 额外伤害 = 全体聚爆上限之和 / 2
        int totalCap = enemies.Sum(e => AemeathFusionBurstState.GetFusionBurstCap(e));
        int bonusDmg = totalCap / 2;
        if (bonusDmg > 0)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;
                await DamageCmd.Attack(bonusDmg).FromCard(this).Targeting(enemy)
                    .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            }
        }
    }

    protected override void OnUpgrade() { }
}
