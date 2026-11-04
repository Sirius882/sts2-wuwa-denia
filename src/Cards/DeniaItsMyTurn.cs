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
        new[] { new DamageVar(15m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_its_my_turn.png";

    public DeniaItsMyTurn()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "到我的回合啦",
            Description: "造成{Damage:diff()}点伤害并附加{IfUpgraded:show:13|10}层聚爆、{IfUpgraded:show:6|3}聚爆上限。抽{IfUpgraded:show:3|2}张牌。若处于黑色形态，切换到粉色形态。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(ctx);
        if (!play.Target.IsDead)
        {
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, IsUpgraded ? 6 : 3, Owner.Creature, this);
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, IsUpgraded ? 13 : 10, Owner.Creature, this);
        }
        await CardPileCmd.Draw(ctx, IsUpgraded ? 3 : 2, Owner);
        if (DeniaFormHelper.IsBlack(Owner.Creature))
            await DeniaFormHelper.SwitchToPink(Owner.Creature, Owner.Creature, this);
    }

    protected override void OnUpgrade() {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
