using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
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
            Description: "敌方仅有一名目标时，获得30层[gold]聚爆轨迹[/gold]，在回合开始时判定。\n聚爆上限引爆后为目标附加其上限一半的聚爆。\n粉色形态下，打出卡牌时，所有敌人+1聚爆+1聚爆上限。",
            Flavor: "如泡沫消解般，褪去梦幻，只留下沉寂的矮星。可即便如此，那曾点亮宇宙的光辉却并未消逝。她静静地等待着，将那借来的光明还给主序星的时刻。");

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner == null) return;
        if (cardPlay.Card.Owner != Owner) return;

        // 所有效果仅在粉色形态触发
        if (!DeniaFormHelper.IsPink(Owner.Creature)) return;

        var enemies = Owner.Creature.CombatState.Enemies.Where(e => !e.IsDead).ToList();
        foreach (var enemy in enemies)
        {
            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 1, Owner.Creature, null!);
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(enemy, 1, Owner.Creature, null!);
        }
    }
}
