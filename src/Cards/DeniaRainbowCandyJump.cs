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
using TuneStrain;

namespace Denia;

/// <summary>彩虹豆豆跳跳糖 — Rare Power, 1e，升级获得固有。粉态攻击牌获得虚质时额外 +2。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaRainbowCandyJump : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? new[] { CardKeyword.Innate }
            : Array.Empty<CardKeyword>();

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_rainbow_candy_jump.png";

    public DeniaRainbowCandyJump()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "彩虹豆豆跳跳糖",
            Description: "通过在[color=#9A6A18]粉色形态[/color]下打出攻击牌获得[color=#9A6A18]虚质[/color]时，额外获得2虚质。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaRainbowCandyJumpPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
public sealed class DeniaRainbowCandyJumpPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_rainbow_candy_jump_power.png";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_rainbow_candy_jump_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
        Title: "彩虹豆豆跳跳糖",
        Description: "通过粉色形态的攻击牌获得虚质时，额外获得{Amount}×2虚质。",
        SmartDescription: "通过粉色形态的攻击牌获得虚质时，额外获得{Amount}×2虚质。");
}
