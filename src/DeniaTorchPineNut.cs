using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>匍炬松松子 — Rare Power, 1e(upg:Innate). Gain 1/5 trajectory as STR on trajectory gain.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTorchPineNut : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded ? new[] { CardKeyword.Innate } : Array.Empty<CardKeyword>();

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_torch_pine_nut.png";

    public DeniaTorchPineNut()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "匍炬松松子",
        Description: "获得[gold]聚爆轨迹[/gold]时，同步获得该次获得层数1/5的[gold]力量[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaTorchPineNutPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
public sealed class DeniaTorchPineNutPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_torch_pine_nut_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_torch_pine_nut_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "匍炬松松子",
            Description: "获得聚爆轨迹时，同步获得该次获得层数1/5的力量。",
            SmartDescription: "获得聚爆轨迹时，同步获得该次获得层数1/5的力量。");

    // ---- 累加器：BeforePowerAmountChanged 通知钩中累加，AfterCardPlayed 安全 flush ----
    private static readonly Dictionary<Creature, int> _pendingStr = new();

    internal static void AccumulateStrength(Creature creature, int amount)
    {
        if (amount <= 0) return;
        lock (_pendingStr)
        {
            _pendingStr.TryGetValue(creature, out int existing);
            _pendingStr[creature] = existing + amount;
        }
    }

    internal static async Task FlushStrengthAsync(Creature creature)
    {
        int amount;
        lock (_pendingStr)
        {
            if (!_pendingStr.TryGetValue(creature, out amount)) return;
            _pendingStr.Remove(creature);
        }
        if (amount > 0)
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                new ThrowingPlayerChoiceContext(), creature, amount, creature, null!);
    }
}
