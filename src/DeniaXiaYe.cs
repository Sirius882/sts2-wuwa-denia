#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>夏耶 — Uncommon Power, 1e. 黯核3: 每次切换形态获得1黯核。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaXiaYe : DeniaCard
{
    public override int CurrentDarkCoreCost => 3;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_xia_ye.png";

    public DeniaXiaYe()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "夏耶",
        Description: "黯核3：每次切换形态时，获得1黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (await TrySpendDarkCore(play))
            await PowerCmd.Apply<DeniaXiaYePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeniaXiaYePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_xia_ye_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_xia_ye_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "夏耶",
            Description: "每次切换形态时，获得黯核。",
            SmartDescription: "每次切换形态时，获得{Amount}黯核。");
}
