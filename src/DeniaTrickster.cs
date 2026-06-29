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
            Description: "敌方仅有一名目标时，获得30层[gold]聚爆轨迹[/gold]，在回合开始时判定。\n任何聚爆上限引爆触发后，为触发的对象附加其[gold]聚爆上限[/gold]四分之一的[gold]聚爆[/gold]。",
            Flavor: "\u201C一个破碎又固执的容器，曾被世界所感知，所塑造\u2014\u2014\u2014\u2014如今它正不断央求着：请您带给我一颗心，随便什么样的心都好。\u201D");
}
