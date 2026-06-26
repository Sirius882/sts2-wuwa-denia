using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaPainkiller : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    protected override string IconBaseName => "denia_painkiller";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "止痛药",
            Description: "每场战斗开始时，获得5点[gold]力量[/gold]、5点[gold]敏捷[/gold]和50层[gold]聚爆轨迹[/gold]。每个回合开始时，获得1层[gold]易伤[/gold]。",
            Flavor: "被残星会会长换成了生理盐水，并没有止痛的效果。");
}
