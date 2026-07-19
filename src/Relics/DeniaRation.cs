using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaRation : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override string IconBaseName => "denia_ration";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "压缩食品",
            Description: "每个回合开始时，获得6点[gold]活力[/gold]。",
            Flavor: "毫无口感和味道可言。实在是难以称之为食物，好在吃下后并没有感到不适。");
}
