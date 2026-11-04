using AemeathWw.Scripts.Api;
using TuneStrain;
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

/// <summary>寒地星苔团 — Rare Attack, X cost, single enemy. VM强化: 3*(y-2)/2 damage, consuming all VM at y>=3.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFrozenStarMossCake : DeniaCard
{
    protected override bool HasEnergyCostX => true;


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
            Description: "附加x点集谐·偏移，无条件谐度破坏x次{IfUpgraded:show:。|，然后失去x点能量。}\n虚质强化：再造成3×(y-2)/2点伤害。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int x = ResolveEnergyXValue();
        int vmBefore = DeniaResourceState.GetVirtualMatter(Owner.Creature);
        bool enhanced = await TrySpendVirtualMatter(play);
        await TuneStrainState.TryAddBias(play.Target, x, Owner.Creature, this);
        for (int i = 0; i < x && !play.Target.IsDead; i++)
            await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        if (!IsUpgraded)
            await PlayerCmd.LoseEnergy(x, Owner);
        if (enhanced && !play.Target.IsDead)
            await DamageCmd.Attack(3m * (vmBefore - 2) / 2m)
                .FromCard(this, play).Targeting(play.Target).Execute(ctx);
    }

    protected override void OnUpgrade()
    {

    }
}
