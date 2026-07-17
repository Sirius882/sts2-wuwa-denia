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

/// <summary>破裂 — Rare Attack, AoE</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaRupture : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_rupture.png";

    public DeniaRupture()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "破裂",
            Description: "对全体敌人造成15点伤害2次。\n再造成等于全体敌人聚爆上限之和的伤害一次。\n黯核强化：每段基础数值+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dcBonus = await TrySpendDarkCore(play) ? 5 : 0;
        int baseDmg = 15 + dcBonus;

        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();

        await DamageCmd.Attack(baseDmg)
            .WithHitCount(2)
            .FromCard(this)
            .TargetingAllOpponents(Owner.Creature.CombatState)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        int totalCap = enemies.Sum(e => AemeathFusionBurstState.GetFusionBurstCap(e));
        int bonusDmg = totalCap + dcBonus;
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

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
