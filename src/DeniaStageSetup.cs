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

namespace Denia;

/// <summary>布景之形 — Uncommon Attack</summary>
public sealed class DeniaStageSetup : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_stage_setup.png";

    public DeniaStageSetup()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dmg = IsUpgraded ? 10 : 7;
        if (await TrySpendDarkCore(play))
            dmg += 7;
        var snapshot = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in snapshot)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(enemy)
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            if (!enemy.IsDead)
            {
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner.Creature, this);
                await AemeathFusionBurstState.TryAddFusionBurst(enemy, 3, Owner.Creature, this);
            }
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "布景之形", Description: "对所有敌人造成{IfUpgraded:show:8|5}点伤害。\n提高[gold]聚爆[/gold]上限3，并附加3点[gold]聚爆[/gold]。\n黯核强化：基础+5。");
}
