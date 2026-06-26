#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>毕业寄语 — Common Skill, 0e. Gain 1 energy. Exhaust. Upg: Retain.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGraduationMessage : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_graduation_message.png";

    public DeniaGraduationMessage()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "毕业寄语",
        Description: "获得1能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PlayerCmd.GainEnergy(1, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
