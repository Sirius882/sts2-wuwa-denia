#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>？！娅娅？！ — Uncommon Attack, resonance break, black form returns to pink.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaYaya : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;


    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_yaya.png";

    public DeniaYaya()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "？！娅娅？！",
            Description: "无条件谐度破坏。若处于黑色形态，切换到粉色。\n虚质强化：再造成4点伤害2次。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool enhanced = await TrySpendVirtualMatter(play);
        await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
        if (enhanced && !play.Target.IsDead)
            await DamageCmd.Attack(4m).WithHitCount(2).FromCard(this, play)
                .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
