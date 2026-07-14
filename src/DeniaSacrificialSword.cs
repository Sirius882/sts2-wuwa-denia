using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Denia;

[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaSacrificialSword : CustomRelicModel
{
    static DeniaSacrificialSword()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaSacrificialSword));
    }

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override string IconBaseName => "denia_sacrificial_sword";

    internal decimal GrantedStrength;
    internal decimal GrantedShroudedStar;
    internal bool EffectRemoved;

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "献斗剑护符",
            Description: "战斗开始时，获得2层[gold]蔽星[/gold]和2点[gold]力量[/gold]。\n第一次失去生命后移除效果。",
            Flavor: "过去献给黄金树的战斗仪式──参考用于仪式中的剑制成的护符。\n战斗仪式在王夫拉达冈的时代受到废除，散布各处的竞技场是遗留下来的产物。");
}
