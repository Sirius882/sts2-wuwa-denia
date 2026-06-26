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
    /// <summary>
    /// 熔毁殆尽填层引爆期间为 true。
    /// 轻唤/熵变强化/回到远方的 Harmony 补丁检查此标志位以跳过副作用。
    /// </summary>
    public static bool IsMeltingAwayBurstFill;

    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_melting_away.png";

    public DeniaMeltingAway()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "熔毁殆尽",
        Description: "对随机一名敌人触发一次无条件引爆。若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]形态，并在此后每回合开始时，对所有敌人附加{IfUpgraded:show:2|1}点[gold]聚爆[/gold]，并提升其聚爆上限1点。\n黯核强化：附加的[gold]聚爆[/gold]层数+2。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 1. 对随机一名敌人触发无条件聚爆上限引爆
        var randomEnemy = DeniaFormHelper.PickRandomEnemy(Owner);
        if (randomEnemy != null)
        {
            IsMeltingAwayBurstFill = true;
            try
            {
                await AemeathFusionBurstState.TryAddFusionBurst(randomEnemy, 40, Owner.Creature, this);
            }
            finally { IsMeltingAwayBurstFill = false; }
        }

        // 2. 黯核强化（在切换形态前消耗，因为黯核需要黑色形态）
        int dcBonus = await TrySpendDarkCore(play) ? 2 : 0;
        int baseAmount = IsUpgraded ? 2 : 1;

        // 3. 若处于黑色形态，切换到粉色，并附加持续性能力
        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

            await PowerCmd.Apply<DeniaMeltingAwayPower>(ctx, Owner.Creature, baseAmount + dcBonus, Owner.Creature, this);
            var appliedPower = Owner.Creature.GetPower<DeniaMeltingAwayPower>();
            if (appliedPower != null) appliedPower.DarkCoreCapBonus += dcBonus;
        }
    }

    protected override void OnUpgrade() { }
}
public sealed class DeniaMeltingAwayPower : BaseLib.Abstracts.CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_melting_away_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_melting_away_power.png";

    /// <summary>黯核强化带来的额外聚爆上限提升量（0 或 2）。</summary>
    public int DarkCoreCapBonus { get; set; }

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "熔毁殆尽", Description: "每回合开始时对所有敌人附加聚爆并提升上限。", SmartDescription: "每回合开始时对所有敌人附加聚爆并提升上限。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return;
        int burst = (int)Amount;
        int cap = 1 + DarkCoreCapBonus;
        var enemies = combatState.Enemies.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in enemies)
        {
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner, null!);
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, cap, Owner, null!);
        }
    }
}
