using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaSacrificialShield : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override string IconBaseName => "denia_sacrificial_shield";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "献斗盾护符",
            Description: "每个回合开始时，获得6点[gold]格挡[/gold]。\n第一次失去生命后移除效果。",
            Flavor: "过去献给黄金树的战斗仪式──参考用于仪式中的盾制成的护符。\n战斗仪式在王夫拉达冈的时代受到废除，散布各处的竞技场是遗留下来的产物。");
}
