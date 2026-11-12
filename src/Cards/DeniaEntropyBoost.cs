using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace Denia;
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaEntropyBoost : DeniaCard
{
    public override int CurrentDarkCoreCost => 2;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_entropy_boost.png";
    public DeniaEntropyBoost() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "熵变强化", Description: "每当自己获得增益或给敌人附加减益时，获得2[color=#9A6A18]格挡[/color]，每回合最多触发6次。不可叠加。\n黯核强化：每次获得的[color=#9A6A18]格挡[/color]+1。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaEntropyBoostPower>() != null) return;

        if (await TrySpendDarkCore(play))
            await PowerCmd.Apply<DeniaEntropyBoostEnhancedPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        else
            await PowerCmd.Apply<DeniaEntropyBoostPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
