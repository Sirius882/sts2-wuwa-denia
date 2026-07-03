using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

/// <summary>到我的回合啦 — Rare Attack.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaItsMyTurn : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(12m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_its_my_turn.png";

    public DeniaItsMyTurn()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "到我的回合啦",
        Description: "造成{Damage:diff()}点伤害并附加8层[gold]聚爆[/gold]。若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]形态，抽{IfUpgraded:show:3|2}张牌。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .Execute(ctx);

        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 8, Owner.Creature, this);

        // 后变身+抽牌（仅黑色形态触发）
        if (DeniaFormHelper.IsBlack(Owner.Creature))
        {
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
            int drawCount = IsUpgraded ? 3 : 2;
            await CardPileCmd.Draw(ctx, drawCount, Owner);
        }
    }

    protected override void OnUpgrade() { }
}
