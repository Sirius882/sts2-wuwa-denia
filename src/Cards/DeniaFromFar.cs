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
            Description: "所有附加聚爆上限的效果额外附加{IfUpgraded:show:2|1}层。触发时，如果目标聚爆上限已满40，改为熔解{IfUpgraded:show:2|1}。不可叠加。\n黯核强化：额外层数和熔解次数都+1。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var existing = Owner.Creature.GetPower<DeniaExtraBurstCapPower>();
        int baseAmount = IsUpgraded ? 2 : 1;
        int current = existing?.ExtraAmount ?? 0;
        if (current >= baseAmount + 1) return;

        int amount = baseAmount + (await TrySpendDarkCore(play) ? 1 : 0);
        if (current >= amount) return;
        if (existing != null) await PowerCmd.Remove(existing);
        await PowerCmd.Apply<DeniaExtraBurstCapPower>(ctx, Owner.Creature, amount, Owner.Creature, this);
    }
    protected override void OnUpgrade() { }
}
