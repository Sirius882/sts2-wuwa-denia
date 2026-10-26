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
            Description: "对随机1名敌人触发1次无条件聚爆上限引爆。若处于黑色形态，切换到粉色形态。此后每个回合开始时，对所有敌人提升聚爆上限1点（不超过硬上限），并附加上限{IfUpgraded:show:1/3|1/4}的聚爆。持续附加聚爆的效果不可叠加。\n黯核强化：上述附加的聚爆层数和聚爆上限都额外+2。");

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

        // 2. 黯核强化（在切换形态前消耗）；持续效果存在时不追加或替换。
        bool applyOngoing = Owner.Creature.GetPower<DeniaMeltingAwayPower>() == null;
        bool enhanced = applyOngoing && await TrySpendDarkCore(play);

        // 3. 若处于黑色形态，切换到粉色，并附加持续性能力。
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        if (!applyOngoing) return;
        if (enhanced && IsUpgraded)
            await PowerCmd.Apply<DeniaMeltingAwayEnhancedPlusPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else if (enhanced)
            await PowerCmd.Apply<DeniaMeltingAwayEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else if (IsUpgraded)
            await PowerCmd.Apply<DeniaMeltingAwayPlusPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else
            await PowerCmd.Apply<DeniaMeltingAwayPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

public class DeniaMeltingAwayPower : BaseLib.Abstracts.CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // 比例分母（4 或 3）和黯核加成由具体状态表示，不累加
    protected virtual int RatioDenominator => 4;
    protected virtual int EnhancementBonus => 0;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_melting_away_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_melting_away_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "熔毁殆尽",
        Description: $"回合开始时对所有敌人提升{1 + EnhancementBonus}点聚爆上限，再附加上限1/{RatioDenominator}的聚爆{(EnhancementBonus > 0 ? $"+{EnhancementBonus}" : "")}。持续效果不可叠加。",
        SmartDescription: $"回合开始时对所有敌人提升{1 + EnhancementBonus}点聚爆上限，再附加上限1/{RatioDenominator}的聚爆{(EnhancementBonus > 0 ? $"+{EnhancementBonus}" : "")}。持续效果不可叠加。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        int ratioDenom = RatioDenominator;
        int dcBonus = EnhancementBonus;
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

public sealed class DeniaMeltingAwayPlusPower : DeniaMeltingAwayPower
{
    protected override int RatioDenominator => 3;
}

public class DeniaMeltingAwayEnhancedPower : DeniaMeltingAwayPower
{
    protected override int EnhancementBonus => 2;
}

public sealed class DeniaMeltingAwayEnhancedPlusPower : DeniaMeltingAwayEnhancedPower
{
    protected override int RatioDenominator => 3;
}
