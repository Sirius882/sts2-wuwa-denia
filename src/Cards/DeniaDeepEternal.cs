using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>深黯、终末、恒常 — Uncommon Attack, AoE</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaDeepEternal : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_deep_eternal.png";

    public DeniaDeepEternal()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "深黯、终末、恒常",
            Description: "对全体敌人提升3点聚爆上限，然后{IfUpgraded:show:对随机2位敌人各触发1次无条件引爆。如果敌方只有1个目标，则将其引爆2次|对随机1位敌人触发1次无条件引爆}。在接下来2回合内，每回合对全体敌人附加上限{IfUpgraded:show:1/2|1/3}的聚爆，并提升3点聚爆上限。若处于黑色形态，切换粉色形态。持续附加聚爆的效果不可叠加。\n黯核强化：回复1能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();
        if (enemies.Length <= 0) return;

        // 1. 全体敌人+3聚爆上限
        foreach (var enemy in enemies)
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner.Creature, this);

        // 2. 无条件引爆（绕开回到远方/从远方/轻唤附加联动）
        var remainingTargets = enemies.ToList();
        int detonations = IsUpgraded ? 2 : 1;
        for (int i = 0; i < detonations; i++)
        {
            var candidates = remainingTargets.Where(e => !e.IsDead).ToArray();
            if (candidates.Length == 0) break;
            var randomEnemy = Owner.RunState.Rng.CombatTargets.NextItem(candidates);
            if (randomEnemy == null) break;
            using (DeniaBurstFillGuard.Enter())
            {
                await AemeathFusionBurstState.TryAddFusionBurst(randomEnemy, 40, Owner.Creature, this);
            }
            if (enemies.Length > 1) remainingTargets.Remove(randomEnemy);
        }

        // 3. 黯核强化（在切换形态前消耗）：回复能量，不改变持续回合。
        if (await TrySpendDarkCore(play))
            await PlayerCmd.GainEnergy(1, Owner);
        if (Owner.Creature.GetPower<DeniaDeepEternalPower>() == null)
        {
            if (IsUpgraded)
                await PowerCmd.Apply<DeniaDeepEternalPlusPower>(ctx, Owner.Creature, 2, Owner.Creature, this);
            else
                await PowerCmd.Apply<DeniaDeepEternalPower>(ctx, Owner.Creature, 2, Owner.Creature, this);
        }

        // 4. 若处于黑色形态，切换到粉色（最后做）
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级提高即时引爆次数和持续聚爆比例。
    }
}

/// <summary>深黯持续效果：每回合先比例聚爆，再提升上限（未升级1/3，升级1/2，并提升3上限）。</summary>
public class DeniaDeepEternalPower : CustomPowerModel
{
    protected virtual int RatioDenominator => 3;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_deep_eternal_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_deep_eternal_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "深黯、终末、恒常",
        Description: $"接下来{{Amount}}回合开始时，对所有敌人附加上限1/{RatioDenominator}的聚爆，并提升3点聚爆上限。持续效果不可叠加。",
        SmartDescription: $"接下来{{Amount}}回合开始时，对所有敌人附加上限1/{RatioDenominator}的聚爆，并提升3点聚爆上限。持续效果不可叠加。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        if (Amount <= 0) return;

        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();

        foreach (var enemy in enemies)
        {
            // 设计文案：先按当前状态附加比例聚爆，再提升3点聚爆上限
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(enemy, 1, RatioDenominator);
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner, null!);
            if (!enemy.IsDead)
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner, null!);
        }

        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null!);
    }
}

public sealed class DeniaDeepEternalPlusPower : DeniaDeepEternalPower
{
    protected override int RatioDenominator => 2;
}
