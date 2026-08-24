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

public sealed class DeniaFakedIllnessTwoDays : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_faked_illness_two_days.png";

    public DeniaFakedIllnessTwoDays()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "多“装”了两天病",
            Description: "只在[color=#9A6A18]黑色形态[/color]下有效。\n给随机敌人附加3点[color=#9A6A18]聚爆[/color]，切换到[color=#9A6A18]粉色形态[/color]。{IfUpgraded:show:获得1黯核。|}\n虚质强化：额外附加3点[color=#9A6A18]聚爆[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (!DeniaFormHelper.IsBlack(Owner.Creature))
            return;

        bool virtualMatter = await TrySpendVirtualMatter(play);

        // 随机敌人
        var enemy = DeniaFormHelper.PickRandomEnemy(Owner);
        if (enemy != null)
        {
            int burst = virtualMatter ? 6 : 3;
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, burst, Owner.Creature, this);
        }

        await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);

        // 升级后获得1黯核
        if (IsUpgraded)
            await DeniaResourceState.GainDarkCore(Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
