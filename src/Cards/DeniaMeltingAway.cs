using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

public sealed class DeniaMeltingAway : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_melting_away.png";

    public DeniaMeltingAway()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "熔毁殆尽",
            Description: "随机引爆一名敌人，切换到[color=#9A6A18]粉色形态[/color]。回合开始时，对所有敌人附加1聚爆上限和上限{IfUpgraded:show:1/3|1/5}的[color=#9A6A18]聚爆[/color]。\n黯核强化：上述附加的[color=#9A6A18]聚爆[/color]层数和聚爆上限都+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 1. 对随机一名敌人触发无条件聚爆上限引爆（绕开回到远方/从远方联动）
        var randomEnemy = DeniaFormHelper.PickRandomEnemy(Owner);
        if (randomEnemy != null)
        {
            using (DeniaBurstFillGuard.Enter())
            {
                await AemeathFusionBurstState.TryAddFusionBurst(randomEnemy, 40, Owner.Creature, this);
            }
        }

        // 2. 黯核强化（在切换形态前消耗）
        int dcBonus = await TrySpendDarkCore(play) ? 2 : 0;
        // Power.Amount = 比例分母（未升级 1/5，升级 1/3）
        int ratioDenom = IsUpgraded ? 3 : 5;

        // 3. 若处于黑色形态，切换到粉色，并附加持续性能力
        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

            await PowerCmd.Apply<DeniaMeltingAwayPower>(ctx, Owner.Creature, ratioDenom, Owner.Creature, this);
            if (dcBonus > 0)
                await PowerCmd.Apply<DeniaMeltingAwayCapBonusPower>(ctx, Owner.Creature, dcBonus, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaMeltingAwayPower : BaseLib.Abstracts.CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // Amount = 比例分母（5 或 3），不累加
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_melting_away_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_melting_away_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "熔毁殆尽",
        Description: "回合开始时对所有敌人提升聚爆上限，再按上限比例附加聚爆。",
        SmartDescription: "回合开始时对所有敌人提升聚爆上限，再按上限比例附加聚爆。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return;
        int ratioDenom = Math.Max(1, (int)Amount);
        int dcBonus = Owner.GetPower<DeniaMeltingAwayCapBonusPower>()?.Amount ?? 0;
        int capGain = 1 + dcBonus;
        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in enemies)
        {
            // 先上限后比例层数（比例按 5+额外上限 向上取整）
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, capGain, Owner, null!);
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(enemy, 1, ratioDenom) + dcBonus;
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner, null!);
        }
    }
}
