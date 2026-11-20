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
using TuneStrain;

namespace Denia;

/// <summary>匍炬松松子 — Rare Power, 1e. Gain double STR on Shrouded Star gain (upgraded: triple).</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTorchPineNut : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_torch_pine_nut.png";

    public DeniaTorchPineNut()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "匍炬松松子",
            Description: "获得[color=#9A6A18]蔽星[/color]时，同步获得{IfUpgraded:show:3|2}倍层数的[color=#9A6A18]力量[/color]。不可叠加。失去蔽星时，不会失去由此获得的力量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaTorchPineNutPower>() != null) return;

        await PowerCmd.Apply<DeniaTorchPineNutPower>(ctx, Owner.Creature, IsUpgraded ? 3 : 2, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaTorchPineNutPower : CustomPowerModel
{
    private int StrengthMultiplier => Math.Max(2, Amount);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_torch_pine_nut_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_torch_pine_nut_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "匍炬松松子",
        Description: "获得蔽星时，同步获得力量。失去蔽星时，不会失去由此获得的力量。",
        SmartDescription: "获得蔽星时，同步获得{Amount}倍层数的力量。失去蔽星时，不会失去由此获得的力量。");

    /// <summary>获得蔽星时立刻同步获得力量（引擎 await AfterPowerAmountChanged）。</summary>
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0 || Owner == null || Owner.IsDead) return;
        if (Amount <= 0) return;
        if (power is not DeniaShroudedStarPower) return;
        if (power.Owner != Owner) return;

        int strGain = (int)amount * StrengthMultiplier;
        if (strGain <= 0) return;

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, strGain, Owner, cardSource);
    }
}
