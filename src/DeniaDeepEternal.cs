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
    public override int CurrentDarkCoreCost => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_deep_eternal.png";

    public DeniaDeepEternal()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "深黯、终末、恒常",
            Description: "提升全体敌人3点聚爆上限。对随机一名敌人触发一次无条件引爆。接下来2回合内，每回合对全体敌人附加3点聚爆并提升3聚爆上限。若处于[gold]黑色形态[/gold]，切换到[gold]粉色形态[/gold]。\n黯核强化：持续回合数变为3。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();
        if (enemies.Length <= 0) return;

        // 1. 全体敌人+3聚爆上限
        foreach (var enemy in enemies)
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner.Creature, this);

        // 2. 无条件引爆
        var randomEnemy = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        await AemeathFusionBurstState.TryAddFusionBurst(randomEnemy, 40, Owner.Creature, this);

        // 3. 黯核强化（在切换形态前消耗）
        int duration = await TrySpendDarkCore(play) ? 3 : 2;
        await PowerCmd.Apply<DeniaDeepEternalPower>(ctx, Owner.Creature, duration, Owner.Creature, this);

        // 4. 若处于[gold]黑色形态[/gold]，切换到粉色（最后做）
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
/// <summary>深黯持续效果：每回合对全体敌人附加聚爆+提升上限。</summary>
public sealed class DeniaDeepEternalPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_deep_eternal_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_deep_eternal_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "深黯、终末、恒常",
            Description: "每回合开始时，对所有敌人附加3点聚爆并提升3点聚爆上限。",
            SmartDescription: "每回合开始时，对所有敌人附加3点聚爆并提升3点聚爆上限。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return;
        if (Amount <= 0) return;

        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();

        foreach (var enemy in enemies)
        {
            // 走 Aemeath 公共附加 API，让“回到远方 / 从远方”的 Harmony 加成生效。
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 3, Owner, null!);
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner, null!);
        }

        // 递减持续回合
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null!);
    }
}
