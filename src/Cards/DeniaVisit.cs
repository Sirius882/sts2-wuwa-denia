#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 谨此致访 — Uncommon Power.
/// 每回合最多受到 MaxTaken 点伤害；每回合对每个怪物最多造成 MaxDealt 点伤害。
/// 负面输出限制只作用于自己，不影响联机队友。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaVisit : DeniaCard
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_visit.png";

    public DeniaVisit() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "谨此致访",
            Description: "本场战斗中，你每回合最多受到{IfUpgraded:show:15|20}点伤害，但是每回合你对每个怪物最多造成{IfUpgraded:show:80|60}点伤害。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int maxTaken = IsUpgraded ? 15 : 20;
        int maxDealt = IsUpgraded ? 80 : 60;
        // Single：图标旁不显示数字；数值全走 Configure 字段
        await PowerCmd.Apply<DeniaVisitPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        var pwr = Owner.Creature.GetPower<DeniaVisitPower>();
        pwr?.Configure(maxTaken, maxDealt);
    }

    protected override void OnUpgrade() { }
}

/// <summary>
/// Single（无层数显示）。上限存在字段里。
/// 用 ModifyDamageCap 做剩余额度上限，AfterDamageReceived / AfterDamageGiven 累计本回合已结算量。
/// </summary>
public sealed class DeniaVisitPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    // Single = 图标旁不标数字（NPower 仅 Counter 显示 Amount）
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/packed/sprite_fonts/denia_energy_icon.png";
    public override string? CustomBigIconPath => "res://images/ui/combat/denia_energy_icon_big.png";

    private int _maxTaken = 20;
    private int _maxDealt = 60;
    private int _takenThisTurn;
    private readonly Dictionary<Creature, int> _dealtThisTurn = new();

    public void Configure(int maxTaken, int maxDealt)
    {
        _maxTaken = maxTaken;
        _maxDealt = maxDealt;
    }

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "谨此致访",
        Description: "你每回合受到的伤害和造成的伤害都有上限。",
        SmartDescription: "你每回合受到的伤害和造成的伤害都有上限。");

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 自己受伤上限：剩余 = maxTaken - takenThisTurn
        if (target == Owner)
        {
            int remaining = _maxTaken - _takenThisTurn;
            if (remaining < 0) remaining = 0;
            return remaining;
        }

        // 自己对某个怪物的输出上限（只限制自己，不影响队友）
        if (dealer == Owner && target != null && !target.IsPlayer)
        {
            _dealtThisTurn.TryGetValue(target, out int dealt);
            int remaining = _maxDealt - dealt;
            if (remaining < 0) remaining = 0;
            return remaining;
        }

        return decimal.MaxValue;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner) return;
        // 累计实际造成的生命伤害（含格挡前的 raw? 设计是“受到伤害”，取 UnblockedDamage 更符合“生命损失”语义；
        // 但“受到N点伤害”在 STS 语境常指 HP 扣减。用 UnblockedDamage。）
        int dmg = (int)result.UnblockedDamage;
        if (dmg <= 0) return;
        _takenThisTurn += dmg;
        await Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target == null || target.IsPlayer) return;
        // 输出限制按 TotalDamage（含被格挡），与 ModifyDamageCap 前的结算量一致
        int dmg = result.TotalDamage;
        if (dmg <= 0) return;
        _dealtThisTurn.TryGetValue(target, out int prev);
        _dealtThisTurn[target] = prev + dmg;
        await Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // 玩家回合开始时重置累计，保证每回合额度刷新
        if (side != CombatSide.Player) return;
        if (Owner == null || !participants.Contains(Owner)) return;
        _takenThisTurn = 0;
        _dealtThisTurn.Clear();
        await Task.CompletedTask;
    }
}
