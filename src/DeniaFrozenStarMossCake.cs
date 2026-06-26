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

/// <summary>寒地星苔团 — Rare Attack, X cost, single enemy. VM强化: +hits based on VM amount.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFrozenStarMossCake : DeniaCard
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(3m, ValueProp.Move) };

    public override int CurrentVirtualMatterCost =>
        DeniaResourceState.GetVirtualMatter(Owner?.Creature!) >= 3
            ? DeniaResourceState.GetVirtualMatter(Owner?.Creature!) : 0;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_frozen_star_moss_cake.png";

    public DeniaFrozenStarMossCake()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "寒地星苔团",
        Description: "造成{Damage:diff()}点伤害2x次。\n虚质强化：若虚质≥3，额外造成{Damage:diff()}点伤害(y-2)/2次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int x = ResolveEnergyXValue();

        // 虚质强化：仅黑色形态，虚质>=3
        int vmBefore = DeniaResourceState.GetVirtualMatter(Owner.Creature);
        bool vmEnhanced = await TrySpendVirtualMatter(play);

        int vmExtraHits = 0;
        if (vmEnhanced && vmBefore >= 3)
            vmExtraHits = (vmBefore - 2) / 2;

        int totalHits = 2 * x + vmExtraHits;
        if (totalHits <= 0) return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(totalHits)
            .FromCard(this)
            .Targeting(play.Target)
            .Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
