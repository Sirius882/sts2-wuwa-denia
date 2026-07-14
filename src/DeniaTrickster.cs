using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[BaseLib.Utils.Pool(typeof(DeniaRelicPool))]
public sealed class DeniaTrickster : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    protected override string IconBaseName => "denia_trickster";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "骗术师",
            Description: "每打出3张牌，给随机一张牌附加临时[gold]集谐响应[/gold]（仅本场战斗存在）。若所有牌都已拥有[gold]集谐响应[/gold]，则跳过这一次。\n任何[gold]聚爆上限[/gold]引爆触发后，为触发的对象附加其[gold]聚爆上限[/gold]四分之一的[gold]聚爆[/gold]（向下取整）。",
            Flavor: "\u201C一个破碎又固执的容器，曾被世界所感知，所塑造\u2014\u2014\u2014\u2014如今它正不断央求着：请您带给我一颗心，随便什么样的心都好。\u201D");
}
