using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTimedRuin : CustomCardModel
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_timed_ruin.png";
    public DeniaTimedRuin() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }
    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(
        Title: "计时的溃灭",
        Description: "所有敌人提高聚爆上限1并附加上限2/5的[gold]聚爆[/gold]。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var e in Owner.Creature.CombatState.Enemies.Where(e2 => !e2.IsDead).ToArray())
        {
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(e, 1, Owner.Creature, this);
            int burst = DeniaFusionBurstMath.CeilRatioOfCap(e, 2, 5);
            if (burst > 0)
                await AemeathFusionBurstState.TryAddFusionBurst(e, burst, Owner.Creature, this);
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
