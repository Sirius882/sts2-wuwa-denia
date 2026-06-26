using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>蕨团蒲藻饼 — Rare Attack, X cost, all enemies. DC: extra 3dmg 3*(y+1) times.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFernAlgaeCake : DeniaCard
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(2m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_fern_algae_cake.png";

    public DeniaFernAlgaeCake()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "蕨团蒲藻饼",
        Description: "对全体敌人造成{Damage:diff()}点伤害x+1次。\n黯核强化：再造成3点伤害3*(y+1)次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int x = ResolveEnergyXValue();
        int dc = DeniaResourceState.GetDarkCore(Owner.Creature);
        int dcExtraHits = dc > 0 ? 3 * (dc + 1) : 0;
        bool dcSpent = dcExtraHits > 0 && await TrySpendDarkCore(play);

        // 基础伤害: 2dmg x+1次
        int baseHits = x + 1;
        if (baseHits > 0)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(baseHits)
                .FromCard(this)
                .TargetingAllOpponents(Owner.Creature.CombatState)
                .Execute(ctx);
        }

        // DC额外伤害: 3dmg 3*(y+1)次
        if (dcSpent && dcExtraHits > 0)
        {
            await DamageCmd.Attack(3m)
                .WithHitCount(dcExtraHits)
                .FromCard(this)
                .TargetingAllOpponents(Owner.Creature.CombatState)
                .Execute(ctx);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
