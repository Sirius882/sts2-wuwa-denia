#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;
using TuneStrain.Powers;

namespace Denia;

/// <summary>吞没 — Common Power: 按集谐响应层数降低集谐·干涉敌人的伤害，不可叠加。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSwallow : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_swallow.png";

    public DeniaSwallow()
        : base(1, CardType.Power, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "吞没",
            Description: "每有一层[color=#9A6A18]集谐响应[/color]power，带有[color=#9A6A18]集谐·干涉[/color]的敌人对你造成的伤害降低{IfUpgraded:show:3|2}%，最多不超过{IfUpgraded:show:45|30}%。此卡不可叠加。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (IsUpgraded)
        {
            // 吞没+优先于吞没：升级牌会将普通状态替换为强化状态。
            if (Owner.Creature.GetPower<DeniaSwallowPower>() != null)
                await PowerCmd.Remove<DeniaSwallowPower>(Owner.Creature);

            if (Owner.Creature.GetPower<DeniaSwallowPlusPower>() == null)
                await PowerCmd.Apply<DeniaSwallowPlusPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

            return;
        }

        // 普通吞没不能覆盖吞没+，且两种状态均不可叠加。
        if (Owner.Creature.GetPower<DeniaSwallowPower>() != null
            || Owner.Creature.GetPower<DeniaSwallowPlusPower>() != null)
            return;

        await PowerCmd.Apply<DeniaSwallowPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

/// <summary>
/// 吞没共享逻辑。两个具体 power 只表示状态是否存在，减伤比例按当前集谐响应层数计算。
/// 仅降低「带有集谐·干涉的敌人对你」造成的伤害；无对外增伤。
/// </summary>
public abstract class DeniaSwallowPowerBase : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    // 复用达妮娅共鸣模态·集谐的图标。
    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";

    protected abstract decimal PercentPerResponse { get; }
    protected abstract decimal MaxReductionPercent { get; }

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 仅 powered 攻击伤害（与集谐干涉易伤同环）。
        if (!props.IsPoweredAttack())
            return 1m;

        // 带有集谐·干涉的敌人对你造成的伤害降低
        if (target == Owner && dealer != null && dealer.GetPower<TuneStrainInterferencePower>() != null)
        {
            int responseLayers = TuneStrainState.GetResponsePowerAmount(Owner);
            decimal reduction = System.Math.Min(MaxReductionPercent, responseLayers * PercentPerResponse);
            return 1m - reduction / 100m;
        }

        return 1m;
    }
}

/// <summary>吞没：每层响应降低 2%，最多 30%。</summary>
public sealed class DeniaSwallowPower : DeniaSwallowPowerBase
{
    protected override decimal PercentPerResponse => 2m;
    protected override decimal MaxReductionPercent => 30m;

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "吞没",
        Description: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低2%，最多不超过30%。",
        SmartDescription: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低2%，最多不超过30%。");
}

/// <summary>吞没+：每层响应降低 3%，最多 45%。</summary>
public sealed class DeniaSwallowPlusPower : DeniaSwallowPowerBase
{
    protected override decimal PercentPerResponse => 3m;
    protected override decimal MaxReductionPercent => 45m;

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "吞没+",
        Description: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低3%，最多不超过45%。",
        SmartDescription: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低3%，最多不超过45%。");
}
