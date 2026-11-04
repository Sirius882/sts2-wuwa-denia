using MegaCrit.Sts2.Core.Commands;
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFirstAndLastGift : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        System.Array.Empty<CardKeyword>();

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_first_and_last_gift.png";

    public DeniaFirstAndLastGift()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "最初和最后的礼物",
            Description: "本场战斗，你每次打出卡牌时，都会给被打出的卡牌附加集谐响应。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaFirstAndLastGiftPower>() == null)
            await PowerCmd.Apply<DeniaFirstAndLastGiftPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
public sealed class DeniaFirstAndLastGiftPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;
    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath => CustomPackedIconPath;
    public override List<(string, string)>? Localization => new PowerLoc(Title: "最初和最后的礼物",
        Description: "每次打出卡牌时，给被打出的卡牌附加集谐响应。",
        SmartDescription: "每次打出卡牌时，给被打出的卡牌附加集谐响应。");
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner == Owner.Player)
            TuneStrainState.AddTemporaryResponse(Owner.Player, play.Card);
        return Task.CompletedTask;
    }
}
