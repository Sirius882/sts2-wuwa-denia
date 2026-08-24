using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>
/// 破裂 — Rare Attack, AoE。
/// 未升级：8×2 + 聚爆上限之和×1；升级：10×2 + 聚爆上限之和×2。
/// 黯核：每段基础 +5（含上限之和段；升级满段理想 +20）。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaRupture : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_rupture.png";

    public DeniaRupture()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "破裂",
            Description: "对全体敌人造成{IfUpgraded:show:10|8}点伤害2次。\n再造成等于全体敌人聚爆上限之和的伤害{IfUpgraded:show:2|1}次。\n黯核强化：每段伤害+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dcBonus = await TrySpendDarkCore(play) ? 5 : 0;
        int baseDmg = (IsUpgraded ? 10 : 8) + dcBonus;
        int capHitCount = IsUpgraded ? 2 : 1;

        var combatState = Owner.Creature.CombatState;
        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();

        await DamageCmd.Attack(baseDmg)
            .WithHitCount(2)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 快照上限之和：各段对每名敌人造成 totalCap+dcBonus（黯核加在每段基础上）
        int totalCap = enemies.Sum(e => AemeathFusionBurstState.GetFusionBurstCap(e));
        int capHitDmg = totalCap + dcBonus;
        if (capHitDmg > 0)
        {
            for (int hit = 0; hit < capHitCount; hit++)
            {
                enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();
                foreach (var enemy in enemies)
                {
                    await DamageCmd.Attack(capHitDmg).FromCard(this).Targeting(enemy)
                        .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
                }
            }
        }
    }
}
