using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaAlbum : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override string IconBaseName => "denia_album";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "相册",
            Description: "粉色形态下，熔解不消耗聚爆层数；黑色形态下，每个回合开始时，获得1能量。",
            Flavor: "在达妮娅曾经的“家”里找到的一本精心呵护的相册。全都是达妮娅。每一张照片里，她都在笑。");
}
