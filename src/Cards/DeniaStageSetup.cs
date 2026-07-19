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

/// <summary>布景之形 — Uncommon Attack</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaStageSetup : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_stage_setup.png";

    public DeniaStageSetup()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dmg = IsUpgraded ? 10 : 7;
        int burst = IsUpgraded ? 4 : 3;
        if (await TrySpendDarkCore(play))
            dmg += 7;
        var snapshot = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in snapshot)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(enemy)
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            if (!enemy.IsDead)
            {
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, burst, Owner.Creature, this);
                await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner.Creature, this);
            }
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "布景之形",
            Description: "对所有敌人造成{IfUpgraded:show:10|7}点伤害，提高[gold]聚爆[/gold]上限{IfUpgraded:show:4|3}，并附加{IfUpgraded:show:4|3}点[gold]聚爆[/gold]。\n黯核强化：基础伤害+7。");
}
