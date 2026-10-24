using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>
/// 蚀刻繁彩 — Uncommon Skill。在敌方生成带爪牙标记的木桩：
/// 木桩受聚爆引爆/谐度破坏伤害翻倍，被玩家击杀则战斗胜利时全队回复。
/// 黯核强化：回复量翻倍（体现在木桩 power 的 Amount 上）。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaEtchedIridescent : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_etched_iridescent.png";

    public DeniaEtchedIridescent() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "蚀刻繁彩",
        Description: "在敌方创建一个{IfUpgraded:show:150|200}生命的木桩。木桩对[color=#9A6A18]聚爆引爆[/color]伤害和[color=#9A6A18]谐度破坏[/color]伤害有2倍易伤。本场战斗结束时，若木桩被玩家击杀而非死于爪牙效果，所有玩家恢复{IfUpgraded:show:15|10}点生命。\n黯核强化：生命恢复量翻倍。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int heal = IsUpgraded ? 15 : 10;
        if (await TrySpendDarkCore(play))
        {
            heal *= 2;
        }

        ICombatState combat = Owner.Creature.CombatState;
        if (combat == null)
        {
            return;
        }

        Creature stake = await CreatureCmd.Add(
            ModelDb.Monster<DeniaEtchingStake>().ToMutable(),
            combat,
            CombatSide.Enemy,
            combat.Encounter?.GetNextSlot(combat));

        if (IsUpgraded)
        {
            // 基础 200 → 升级 150：按 3/4 比例收缩，兼容多人血量缩放。
            stake.SetMaxHpInternal(stake.MaxHp * 3m / 4m);
        }

        await PowerCmd.Apply<MinionPower>(ctx, stake, 1m, Owner.Creature, this);
        await PowerCmd.Apply<DeniaEtchingStakePower>(ctx, stake, heal, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
