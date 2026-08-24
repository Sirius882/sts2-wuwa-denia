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

/// <summary>寒地星苔团 — Rare Attack, X cost, single enemy. VM强化: +hits based on VM amount, ceil((y-2)/4).</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFrozenStarMossCake : DeniaCard
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(6m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_frozen_star_moss_cake.png";

    public override int CurrentVirtualMatterCost
    {
        get
        {
            if (!TryGetOwner(out var owner)) return 0;
            int virtualMatter = DeniaResourceState.GetVirtualMatter(owner!.Creature);
            return virtualMatter >= 3 ? virtualMatter : 0;
        }
    }

    public DeniaFrozenStarMossCake()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "寒地星苔团",
            Description: "造成{Damage:diff()}点伤害x次。\n虚质强化：若虚质≥3，额外造成{Damage:diff()}点伤害(y-2)/4次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int x = ResolveEnergyXValue();

        int vmBefore = DeniaResourceState.GetVirtualMatter(Owner.Creature);
        bool vmEnhanced = await TrySpendVirtualMatter(play);

        int vmExtraHits = 0;
        if (vmEnhanced && vmBefore >= 3)
            vmExtraHits = (int)Math.Ceiling((vmBefore - 2) / 4.0);

        int totalHits = x + vmExtraHits;
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
