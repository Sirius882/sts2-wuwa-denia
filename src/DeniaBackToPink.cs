using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

public sealed class DeniaBackToPink : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_back_to_pink.png";

    public DeniaBackToPink()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, showInCardLibrary: false) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "好女孩？",
        Description: "只在[gold]黑色[/gold]形态下有效。\n给随机敌人附加5点[gold]聚爆[/gold]。\n切换到[gold]粉色[/gold]形态。{IfUpgraded:show:获得1黯核。|}\n黯核强化：恢复1点能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature))
            return;

        // 黯核强化判定必须在黑色形态下先完成，否则切粉后无法触发
        bool darkCore = await TrySpendDarkCore(play);

        // 随机敌人
        var enemy = DeniaFormHelper.PickRandomEnemy(Owner);
        if (enemy != null)
        {
            // 黯核强化：先附加聚爆上限，再附加聚爆（同一目标）
            if (darkCore)
                await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 3, Owner.Creature, this);
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 5, Owner.Creature, this);
        }

        await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        // 升级后获得1黯核
        if (IsUpgraded)
            await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
