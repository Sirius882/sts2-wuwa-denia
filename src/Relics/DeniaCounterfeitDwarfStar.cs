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
public sealed class DeniaCounterfeitDwarfStar : DeniaResourceRelic
{
    protected override int PinkVirtualMatter => 2;
    protected override int ResponseThreshold => 2;
    protected override bool GainDarkCoreInBlack => true;

    public override RelicRarity Rarity => RelicRarity.Ancient;
    protected override string IconBaseName => "denia_counterfeit_dwarf_star";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "赝作的矮星",
            Description: "黑色切换粉色时，若切换前拥有7虚质，抽1张牌。\n由粉色切换到黑色时，获得10点虚质。粉色形态下，打出攻击牌或触发熔解/聚爆引爆时，获得2点虚质。由黑色切换到粉色时归零。\n黯核：玩家的回合开始时，黯核+1。\n一场战斗中，每打出2张牌，给随机一张牌附加临时[gold]集谐响应[/gold]。打出攻击牌的，计为打出两张牌。",
            Flavor: "如泡沫消解般，褪去梦幻，只留下沉寂的矮星。可即便如此，那曾点亮宇宙的光辉却并未消逝。她静静地等待着，将那借来的光明还给主序星的时刻。");
}
