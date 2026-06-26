using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>蚀刻繁彩 — Common Attack</summary>
public sealed class DeniaEtchedIridescent : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_etched_iridescent.png";

    public DeniaEtchedIridescent() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "蚀刻繁彩",
        Description: "附加{IfUpgraded:show:6|3}点[gold]聚爆[/gold]，抽1张牌。\n黯核强化：再附加3点[gold]聚爆[/gold]2次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int b = IsUpgraded ? 6 : 3;
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, b, Owner.Creature, this);
        await CardPileCmd.Draw(ctx, 1, Owner);

        if (await TrySpendDarkCore(play))
        {
            for (int i = 0; i < 2; i++)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 3, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
