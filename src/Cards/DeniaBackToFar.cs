using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

public sealed class DeniaBackToFar : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_back_to_far.png";
    public DeniaBackToFar() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "回到远方",
            Description: "所有附加[color=#9A6A18]聚爆[/color]的效果额外附加触发时目标聚爆上限1/10的层数。\n黯核强化：额外层数变为上限1/7。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // Amount 存比例分母：1/10 或黯核强化 1/7
        int ratioDenom = await TrySpendDarkCore(play) ? 7 : 10;
        await PowerCmd.Apply<DeniaExtraBurstPower>(ctx, Owner.Creature, ratioDenom, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
