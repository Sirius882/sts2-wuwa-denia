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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>光辉，自此消融 — Rare Finisher Attack, X cost, all enemies.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaRadianceDissolved : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(0m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_radiance_dissolved.png";

    public DeniaRadianceDissolved()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "光辉，自此消融",
        Description: "无论形态，消耗所有[gold]虚质[/gold]和黯核。对全体敌人造成y/2+4z+力量点伤害x次。若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]形态。\n打出此牌后，若没有在{IfUpgraded:show:3|2}回合内获胜，给自己附加80层灾厄。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int energyX = ResolveEnergyXValue();
        int vmSpent = DeniaResourceState.GetVirtualMatter(Owner.Creature);
        int dcSpent = DeniaResourceState.GetDarkCore(Owner.Creature);

        await DeniaResourceState.ClearVirtualMatter(Owner.Creature, Owner.Creature, this);
        // 直接清零黯核，绕过 TrySpendDarkCore 的黑色形态检查
        if (dcSpent > 0)
        {
            if (Owner.Creature.Player?.PlayerCombatState != null)
                await PlayerCmd.SetStars(0, Owner.Creature.Player);
            var dcPower = Owner.Creature.GetPower<DeniaDarkCorePower>();
            if (dcPower != null)
                await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), dcPower, -(decimal)dcSpent, Owner.Creature, this);
        }

        int hits = energyX;

        decimal dmg = vmSpent + 8 * dcSpent;
        if (hits > 0 && dmg > 0)
        {
            await DamageCmd.Attack(dmg)
                .WithHitCount(hits)
                .FromCard(this)
                .TargetingAllOpponents(Owner.Creature.CombatState)
                .Execute(ctx);
        }

        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
        }

        // 灾厄倒计时
        await PowerCmd.Apply<DeniaCataclysmTimerPower>(ctx, Owner.Creature, IsUpgraded ? 3 : 2, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
public sealed class DeniaCataclysmTimerPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "灾厄倒数",
            Description: "回合结束后若未获胜，给自己附加80层灾厄。",
            SmartDescription: "{Amount}回合后若未获胜，给自己附加80层灾厄。");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return;
        if (Amount <= 0) return;

        decimal remaining = Amount - 1m;
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null!);

        if (remaining <= 0m)
        {
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.DoomPower>(new ThrowingPlayerChoiceContext(), Owner, 80, Owner, null!);
            await PowerCmd.Remove<DeniaCataclysmTimerPower>(Owner);
        }
    }
}
