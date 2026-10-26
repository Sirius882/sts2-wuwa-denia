using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFromFar : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_from_far.png";
    public DeniaFromFar() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "从远方",
            Description: "所有附加聚爆上限的效果额外附加1层。触发时，如果目标聚爆上限已满40，改为熔解1。不可叠加，可以覆盖方式升级。\n黯核强化：额外层数和熔解次数都变为2。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var existing = Owner.Creature.GetPower<DeniaExtraBurstCapPower>();
        if (existing is DeniaExtraBurstCapEnhancedPower) return;
        if (existing != null)
        {
            if (!await TrySpendDarkCore(play)) return;
            await PowerCmd.Remove(existing);
            await PowerCmd.Apply<DeniaExtraBurstCapEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
            return;
        }

        if (await TrySpendDarkCore(play))
            await PowerCmd.Apply<DeniaExtraBurstCapEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else
            await PowerCmd.Apply<DeniaExtraBurstCapPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
