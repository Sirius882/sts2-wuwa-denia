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
            Description: "所有附加聚爆的效果额外附加{IfUpgraded:show:2+|}触发时目标聚爆上限1/10的层数。不可叠加。\n黯核强化：比例变为1/7上限。");
    // 比例分母由具体 power 状态表示：1/10 或黯核强化 1/7。
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var existing = Owner.Creature.GetPower<DeniaExtraBurstPower>();
        bool upgraded = IsUpgraded || existing?.FlatBonus > 0;
        bool enhanced = existing?.RatioDenominator == 7;
        if (existing != null && existing.FlatBonus == (upgraded ? 2 : 0) && enhanced) return;
        if (!enhanced) enhanced = await TrySpendDarkCore(play);
        if (existing != null && existing.FlatBonus == (upgraded ? 2 : 0)
            && existing.RatioDenominator == (enhanced ? 7 : 10)) return;
        if (existing != null) await PowerCmd.Remove(existing);
        if (upgraded && enhanced)
            await PowerCmd.Apply<DeniaExtraBurstUpgradedEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else if (upgraded)
            await PowerCmd.Apply<DeniaExtraBurstUpgradedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else if (enhanced)
            await PowerCmd.Apply<DeniaExtraBurstEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else
            await PowerCmd.Apply<DeniaExtraBurstPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() { }
}
