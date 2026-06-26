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

namespace Denia;

/// <summary>到我的回合啦 — Rare Skill, 1e. Switch to pink, draw, random multi-hit+burst.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaItsMyTurn : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(3m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_its_my_turn.png";

    public DeniaItsMyTurn()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "到我的回合啦",
        Description: "对随机敌人造成{Damage:diff()}点伤害并附加2点[gold]聚爆[/gold]4次。\n若处于[gold]黑色[/gold]形态，切换到[gold]粉色[/gold]形态，抽{IfUpgraded:show:3|2}张牌。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 先打出4段随机伤害+聚爆（不论形态）
        for (int i = 0; i < 4; i++)
        {
            var enemy = DeniaFormHelper.PickRandomEnemy(Owner);
            if (enemy == null) continue;

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this)
                .Targeting(enemy)
                .Execute(ctx);

            await AemeathFusionBurstState.TryAddFusionBurst(enemy, 2, Owner.Creature, this);
        }

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
