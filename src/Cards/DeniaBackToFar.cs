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
            Description: "所有附加聚爆的效果额外附加触发时目标聚爆上限1/10的层数。不可叠加，可以覆盖方式升级。\n黯核强化：额外层数变为1/7上限。");
    // 比例分母由具体 power 状态表示：1/10 或黯核强化 1/7。
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var existing = Owner.Creature.GetPower<DeniaExtraBurstPower>();
        if (existing is DeniaExtraBurstEnhancedPower) return;
        if (existing != null)
        {
            if (!await TrySpendDarkCore(play)) return;
            await PowerCmd.Remove(existing);
            await PowerCmd.Apply<DeniaExtraBurstEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
            return;
        }

        if (await TrySpendDarkCore(play))
            await PowerCmd.Apply<DeniaExtraBurstEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else
            await PowerCmd.Apply<DeniaExtraBurstPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
