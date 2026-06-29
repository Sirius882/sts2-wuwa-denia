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
            Description: "提升全体敌人5点聚爆上限。对目标触发一次无条件引爆。接下来{IfUpgraded:show:3|2}回合内，每回合对全体敌人附加5点聚爆并提升5聚爆上限。若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]形态。\n黯核强化：持续回合数+1。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToArray();

        // 1. 全体敌人+5聚爆上限
        foreach (var enemy in enemies)
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 5, Owner.Creature, this);

        // 2. 无条件引爆（绕过联动效果）
        DeniaMeltingAway.IsMeltingAwayBurstFill = true;
        try
        {
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 40, Owner.Creature, this);
        }
        finally { DeniaMeltingAway.IsMeltingAwayBurstFill = false; }

        // 3. 黯核强化（在切换形态前消耗）
        int dcBonus = await TrySpendDarkCore(play) ? 1 : 0;
        int duration = 2 + dcBonus;
        await PowerCmd.Apply<DeniaDeepEternalPower>(ctx, Owner.Creature, duration, Owner.Creature, this);

        // 4. 若处于黑色形态，切换到粉色（最后做）
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
            Description: "每回合开始时，对所有敌人附加5点聚爆并提升5点聚爆上限。",
            SmartDescription: "每回合开始时，对所有敌人附加5点聚爆并提升5点聚爆上限。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return;
        if (Amount <= 0) return;

        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();

        // 使用 IsMeltingAwayBurstFill 绕过轻唤/熵变强化/回到远方等联动
        DeniaMeltingAway.IsMeltingAwayBurstFill = true;
        try
        {
            foreach (var enemy in enemies)
            {
                await AemeathFusionBurstState.TryAddFusionBurst(enemy, 5, Owner, null!);
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 5, Owner, null!);
            }
        }
        finally { DeniaMeltingAway.IsMeltingAwayBurstFill = false; }

        // 递减持续回合
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null!);
    }
}
