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

/// <summary>幻沫 — Uncommon Power。触发虚质强化时获得格挡，每回合有限次。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPhantomFoam : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_phantom_foam.png";

    public DeniaPhantomFoam()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "幻沫",
            Description: "触发[color=#9A6A18]虚质强化[/color]时，获得3点[color=#9A6A18]格挡[/color]。每回合最多触发{IfUpgraded:show:5|3}次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = play;
        int max = IsUpgraded ? 5 : 3;
        var existing = Owner.Creature.GetPower<DeniaPhantomFoamPower>();
        if (existing == null)
        {
            var applied = await PowerCmd.Apply<DeniaPhantomFoamPower>(ctx, Owner.Creature, 3m, Owner.Creature, this);
            if (applied is DeniaPhantomFoamPower p) p.MaxTriggers = max;
        }
        else
        {
            existing.MaxTriggers = System.Math.Max(existing.MaxTriggers, max);
            if (existing.Amount < 3)
                await PowerCmd.ModifyAmount(ctx, existing, 3m - existing.Amount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaPhantomFoamPower : CustomPowerModel
{
    public int MaxTriggers = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    // 借用原版 next-turn block 图标
    public override string? CustomPackedIconPath => "res://images/powers/block_next_turn_power.png";
    public override string? CustomBigIconPath => "res://images/powers/block_next_turn_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "幻沫",
        Description: "触发虚质强化时，获得格挡。每回合有触发上限。",
        SmartDescription: "触发虚质强化时，获得{Amount}点格挡。每回合有触发上限。");

    public static async Task OnVirtualMatterEnhanced(Creature creature)
    {
        var power = creature.GetPower<DeniaPhantomFoamPower>();
        if (power == null || power.Amount <= 0) return;

        var triggered = creature.GetPower<DeniaPhantomFoamTriggeredThisTurnPower>();
        int used = (int)(triggered?.Amount ?? 0);
        if (used >= power.MaxTriggers) return;

        await PowerCmd.Apply<DeniaPhantomFoamTriggeredThisTurnPower>(
            new ThrowingPlayerChoiceContext(), creature, 1m, creature, null!);
        // Move：受敏捷等格挡加成（不要用 Unpowered）
        await CreatureCmd.GainBlock(
            creature, new BlockVar(power.Amount, ValueProp.Move), null);
    }
}
