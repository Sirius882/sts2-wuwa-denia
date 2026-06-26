using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>
/// 虚质科学直觉 — Ancient Power. 仅由尘封魔典获得。
/// autoAdd:false 不入随机池；showInCardLibrary:true 图鉴可见。
/// [Pool] 由 DeniaCard 基类继承，子类不重复声明。
/// </summary>
public sealed class DeniaVirtualScienceIntuition : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_virtual_science_intuition.png";

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public DeniaVirtualScienceIntuition()
        : base(1, CardType.Power, CardRarity.Ancient, TargetType.Self, showInCardLibrary: true) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "虚质科学直觉",
        Description: "本场战斗每消耗10点[gold]虚质[/gold]，获得1点[gold]能量[/gold]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaVirtualScienceIntuitionPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
