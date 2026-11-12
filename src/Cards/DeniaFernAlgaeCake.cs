using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>蕨团蒲藻饼 — Rare Attack, X cost, all enemies.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFernAlgaeCake : DeniaCard
{
    protected override bool HasEnergyCostX => true;
    public override int CurrentDarkCoreCost => TryGetOwner(out var owner)
        ? DeniaResourceState.GetDarkCore(owner!.Creature) : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(7m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fern_algae_cake.png";

    public DeniaFernAlgaeCake()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "蕨团蒲藻饼",
            Description: "对全体敌人造成7×{IfUpgraded:show:(x+1)|x}点伤害。\n黯核强化：再造成9y点伤害，与主效果合并结算一次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int x = ResolveEnergyXValue();
        int dc = DeniaResourceState.GetDarkCore(Owner.Creature);
        bool enhanced = dc > 0 && !play.IsAutoPlay && DeniaFormHelper.IsBlack(Owner.Creature);
        if (enhanced)
        {
            await DeniaResourceState.ClearDarkCore(Owner.Creature, Owner.Creature, this);
            await DeniaEnhancementEvents.NotifyDarkCoreEnhanced(Owner.Creature);
        }
        decimal damage = DynamicVars.Damage.BaseValue * (x + (IsUpgraded ? 1 : 0)) + (enhanced ? 9m * dc : 0m);
        if (damage <= 0) return;
        await DamageCmd.Attack(damage).FromCard(this, play)
            .TargetingAllOpponents(Owner.Creature.CombatState).Execute(ctx);
    }

    protected override void OnUpgrade()
    {

    }
}
