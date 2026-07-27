using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

[BaseLib.Utils.Pool(typeof(DeniaRelicPool))]
public sealed class DeniaCounterfeitDwarfStar : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    protected override string IconBaseName => "denia_counterfeit_dwarf_star";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "赝作的矮星",
            Description: "每打出2张牌，给随机一张牌附加临时[gold]集谐响应[/gold]（仅本场战斗存在）。若所有牌都已拥有[gold]集谐响应[/gold]，则跳过这一次。\n任何[gold]聚爆上限[/gold]引爆触发后，为触发的对象附加其[gold]聚爆上限[/gold]三分之一的[gold]聚爆[/gold]（向上取整）。",
            Flavor: "如泡沫消解般，褪去梦幻，只留下沉寂的矮星。可即便如此，那曾点亮宇宙的光辉却并未消逝。她静静地等待着，将那借来的光明还给主序星的时刻。");
}
