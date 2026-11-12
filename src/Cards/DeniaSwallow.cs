#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
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

/// <summary>吞没 — Uncommon Power: 按集谐响应层数降低集谐·干涉敌人的伤害，不可叠加。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSwallow : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_swallow.png";

    public DeniaSwallow()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "吞没",
            Description: "{IfUpgraded:show:卡组中每有一个集谐响应标记、每有一层集谐响应power，敌人对你造成的伤害降低4%，最多不超过60%。在每个我方回合结束时统计。|每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低3%，最多不超过45%。}不可叠加。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaSwallowPowerBase>() != null) return;

        // 首次生效的是吞没+时，使用强化状态；入口守卫已排除普通状态。
        if (IsUpgraded)
        {
            await PowerCmd.Apply<DeniaSwallowPlusPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
            await Owner.Creature.GetPower<DeniaSwallowPlusPower>()!.RefreshSnapshot(ctx);
        }
        // 普通吞没和吞没+均保留本场战斗首次生效的状态。
        else
            await PowerCmd.Apply<DeniaSwallowPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

/// <summary>
/// 吞没共享逻辑。基础版即时读取响应层数；升级版读取回合末快照。
/// 基础版要求敌人带有集谐·干涉；升级版适用于所有敌人。无对外增伤。
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
    protected virtual int ResponseLayers => TuneStrainState.GetResponsePowerAmount(Owner);
    protected virtual bool AppliesTo(Creature dealer) => dealer.GetPower<TuneStrainInterferencePower>() != null;

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 仅 powered 攻击伤害（与集谐干涉易伤同环）。
        if (!props.IsPoweredAttack())
            return 1m;

        // 带有集谐·干涉的敌人对你造成的伤害降低
        if (target == Owner && dealer != null && AppliesTo(dealer))
        {
            int responseLayers = ResponseLayers;
            decimal reduction = System.Math.Min(MaxReductionPercent, responseLayers * PercentPerResponse);
            return 1m - reduction / 100m;
        }

        return 1m;
    }
}

/// <summary>吞没：每层响应降低 3%，最多 45%。</summary>
public sealed class DeniaSwallowPower : DeniaSwallowPowerBase
{
    protected override decimal PercentPerResponse => 3m;
    protected override decimal MaxReductionPercent => 45m;
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "吞没",
        Description: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低3%，最多不超过45%。不可叠加。",
        SmartDescription: "每有一层集谐响应power，带有集谐·干涉的敌人对你造成的伤害降低3%，最多不超过45%。不可叠加。");
}

/// <summary>吞没+：按回合末的响应标记与 power 层数之和减伤。</summary>
public sealed class DeniaSwallowPlusPower : DeniaSwallowPowerBase
{
    protected override decimal PercentPerResponse => 4m;
    protected override decimal MaxReductionPercent => 60m;
    protected override int ResponseLayers => (Owner.GetPower<DeniaSwallowSnapshotPower>()?.Amount ?? 1) - 1;
    protected override bool AppliesTo(Creature dealer) => !dealer.IsPlayer;
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "吞没+",
        Description: "卡组中每有一个集谐响应标记、每有一层集谐响应power，敌人对你造成的伤害降低4%，最多不超过60%。在每个我方回合结束时统计。不可叠加。",
        SmartDescription: "卡组中每有一个集谐响应标记、每有一层集谐响应power，敌人对你造成的伤害降低4%，最多不超过60%。在每个我方回合结束时统计。不可叠加。");

    public async Task RefreshSnapshot(PlayerChoiceContext ctx)
    {
        int markers = new[] { PileType.Hand, PileType.Draw, PileType.Discard }
            .SelectMany(pile => pile.GetPile(Owner.Player!).Cards)
            .Count(card => card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        int layers = markers + TuneStrainState.GetResponsePowerAmount(Owner);
        await PowerCmd.Remove<DeniaSwallowSnapshotPower>(Owner);
        await PowerCmd.Apply<DeniaSwallowSnapshotPower>(ctx, Owner, layers + 1, Owner, null);
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Player ? RefreshSnapshot(ctx) : Task.CompletedTask;
}

public sealed class DeniaSwallowSnapshotPower : DeniaHiddenCounterPower { }
