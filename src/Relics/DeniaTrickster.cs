using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace Denia;

[BaseLib.Utils.Pool(typeof(DeniaRelicPool))]
public sealed class DeniaTrickster : DeniaResourceRelic
{
    protected override int PinkVirtualMatter => 1;
    protected override int ResponseThreshold => 3;

    public override RelicRarity Rarity => RelicRarity.Starter;
    protected override string IconBaseName => "denia_trickster";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "骗术师",
            Description: "黑色切换粉色时，若切换前拥有7虚质，抽1张牌。\n由粉色切换到黑色时，获得10点虚质。粉色形态下，打出攻击牌或触发熔解/聚爆引爆时，获得1点虚质。由黑色切换到粉色时归零。\n黯核：玩家的回合开始时，如果处于粉色形态，黯核+1。\n一场战斗中，每打出3张牌，给随机一张牌附加临时[gold]集谐响应[/gold]。打出攻击牌的，计为打出两张牌。",
            Flavor: "\u201C一个破碎又固执的容器，曾被世界所感知，所塑造\u2014\u2014\u2014\u2014如今它正不断央求着：请您带给我一颗心，随便什么样的心都好。\u201D");
}
