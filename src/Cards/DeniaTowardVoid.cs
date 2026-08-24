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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>向虚而行 — Uncommon Power。触发黯核强化时获得格挡，每回合最多4次。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTowardVoid : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_toward_void.png";

    public DeniaTowardVoid()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "向虚而行",
            Description: "触发[color=#9A6A18]黯核强化[/color]时，获得{IfUpgraded:show:3|2}点[color=#9A6A18]格挡[/color]。每回合最多触发4次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = play;
        int block = IsUpgraded ? 3 : 2;
        var existing = Owner.Creature.GetPower<DeniaTowardVoidPower>();
        if (existing == null)
            await PowerCmd.Apply<DeniaTowardVoidPower>(ctx, Owner.Creature, block, Owner.Creature, this);
        else if (existing.Amount < block)
            await PowerCmd.ModifyAmount(ctx, existing, block - existing.Amount, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaTowardVoidPower : CustomPowerModel
{
    private const int MaxTriggersPerTurn = 4;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    // 借用 entropy boost 风格的格挡相关图标（本 mod 现有资源）
    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_entropy_boost_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "向虚而行",
        Description: "触发黯核强化时，获得格挡。每回合最多触发4次。",
        SmartDescription: "触发黯核强化时，获得{Amount}点格挡。每回合最多触发4次。");

    public static async Task OnDarkCoreEnhanced(Creature creature)
    {
        var power = creature.GetPower<DeniaTowardVoidPower>();
        if (power == null || power.Amount <= 0) return;

        var triggered = creature.GetPower<DeniaTowardVoidTriggeredThisTurnPower>();
        int used = (int)(triggered?.Amount ?? 0);
        if (used >= MaxTriggersPerTurn) return;

        await PowerCmd.Apply<DeniaTowardVoidTriggeredThisTurnPower>(
            new ThrowingPlayerChoiceContext(), creature, 1m, creature, null!);
        // Move：受敏捷等格挡加成（与幻沫一致，不要用 Unpowered）
        await CreatureCmd.GainBlock(
            creature, new BlockVar(power.Amount, ValueProp.Move), null);
    }
}
