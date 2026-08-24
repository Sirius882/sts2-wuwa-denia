using System;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>蚀刻繁彩 — Common Attack</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaEtchedIridescent : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_etched_iridescent.png";

    public DeniaEtchedIridescent() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "蚀刻繁彩",
            Description: "[color=#9A6A18]熔解[/color]1，附加上限{IfUpgraded:show:3/5|1/4}的[color=#9A6A18]聚爆[/color]。抽{IfUpgraded:show:2|1}张牌。\n黯核强化：主效果每使你抽1张牌，再附加3[color=#9A6A18]聚爆[/color]1次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int num = IsUpgraded ? 3 : 1;
        int den = IsUpgraded ? 5 : 4;
        int draw = IsUpgraded ? 2 : 1;

        await AemeathFusionBurstState.ResolveMelt(play.Target, Owner.Creature, this, 1);
        if (!play.Target.IsDead)
        {
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(play.Target, num, den);
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
        }
        await CardPileCmd.Draw(ctx, draw, Owner);

        // 黯核强化：升级前1次、升级后2次，分段附加聚爆
        if (await TrySpendDarkCore(play) && !play.Target.IsDead)
        {
            for (int i = 0; i < draw; i++)
                await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 3, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}
