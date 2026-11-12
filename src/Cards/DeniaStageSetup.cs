using MegaCrit.Sts2.Core.Models;
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
    public override int CurrentDarkCoreCost => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    private bool CanEnhance => TryGetOwner(out var owner)
        && DeniaFormHelper.IsBlack(owner!.Creature)
        && DeniaResourceState.GetDarkCore(owner.Creature) >= CurrentDarkCoreCost;

    protected override bool ShouldGlowGoldInternal => CanEnhance;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card != this || !CanEnhance) return false;
        modifiedCost = System.Math.Max(0m, originalCost - 1m);
        return true;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext ctx, PowerModel power, decimal amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source)
    {
        if (power.Owner == Owner.Creature && power is DeniaDarkCorePower or DeniaFormPower)
            InvokeEnergyCostChanged();
        return Task.CompletedTask;
    }

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_stage_setup.png";

    public DeniaStageSetup()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int dmg = IsUpgraded ? 13 : 10;
        int capGain = IsUpgraded ? 7 : 5;
        await TrySpendDarkCore(play);
        var snapshot = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in snapshot)
        {
            await DamageCmd.Attack(dmg).FromCard(this, play).Targeting(enemy)
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            if (!enemy.IsDead)
            {
                // 先上限，再附加固定聚爆（未升级 6，升级 9）
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, capGain, Owner.Creature, this);
                int burst = IsUpgraded ? 9 : 6;
                if (burst > 0)
                    await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner.Creature, this);
            }
        }
    }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "布景之形",
            Description: "对所有敌人造成{IfUpgraded:show:13|10}点伤害，提高聚爆上限{IfUpgraded:show:7|5}，然后附加{IfUpgraded:show:9|6}聚爆。\n黯核强化：能耗-1。");
}
