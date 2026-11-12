using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

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

    // 木桩生命显示变量：数值随人数与幕缩放（与实际结算一致），升级预览走独立变量，
    // 否则未升级卡在预览分支里读不到升级后的数值。
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new StakeHpVar("StakeHp", assumeUpgraded: false),
        new StakeHpVar("StakeHpUpgraded", assumeUpgraded: true),
    };

    public override List<(string, string)>? Localization => new CardLoc(Title: "蚀刻繁彩",
        Description: "在敌方创建一个{IfUpgraded:show:{StakeHpUpgraded}|{StakeHp}}生命的木桩。木桩对[color=#9A6A18]聚爆引爆[/color]伤害和[color=#9A6A18]谐度破坏[/color]伤害有2倍易伤。木桩被玩家击杀时，所有玩家立即恢复{IfUpgraded:show:15|10}点生命。\n黯核强化：生命恢复量翻倍。");

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

        // 没有槽位 scene 的遭遇（EncounterModel.HasScene 默认 false，含第三方 mod 的战斗）
        // 或槽位已被占满时，GetNextSlot 返回空串；CreatureCmd.Add 拿到空串槽名会让
        // NCombatRoom.AddCreature 抛 "slot name '' but EncounterSlots is null" 并卡死出牌流程。
        // 引擎认可的空槽路径是 null（CreatureCmd.Add 的默认参数）：先入場，再手动摆放位置。
        string? slot = combat.Encounter?.GetNextSlot(combat);
        if (string.IsNullOrEmpty(slot))
        {
            slot = null;
        }

        Creature stake = await CreatureCmd.Add(
            ModelDb.Monster<DeniaEtchingStake>().ToMutable(),
            combat,
            CombatSide.Enemy,
            slot);

        if (slot == null)
        {
            PositionStakeWithoutSlot(combat, stake);
        }

        if (IsUpgraded)
        {
            // 基础 200 → 升级 150：按 3/4 比例收缩，兼容多人血量缩放。
            stake.SetMaxHpInternal(stake.MaxHp * 3m / 4m);
        }

        await PowerCmd.Apply<MinionPower>(ctx, stake, 1m, Owner.Creature, this);
        await PowerCmd.Apply<DeniaEtchingStakePower>(ctx, stake, heal, Owner.Creature, this);
        // 回血载体挂玩家侧（木桩死亡即被移出战斗名单，其 power 收不到结算钩子）：
        // 击杀当场结算，见 DeniaEtchingStakeHealPower。重复打出时 Single 叠层只合并
        // Amount，回血单价存 HealPerStake（每次以最新一发为准）。
        await PowerCmd.Apply<DeniaEtchingStakeHealPower>(ctx, Owner.Creature, heal, Owner.Creature, this);
        Owner.Creature.GetPower<DeniaEtchingStakeHealPower>()!.HealPerStake = heal;
    }

    /// <summary>
    /// 无槽位战斗中手动摆放木桩：放到最右侧存活敌人的右边，超出屏宽则改放最左侧敌人的左边。
    /// 读写的是各敌人节点在同一父容器（_enemyContainer）下的局部 <see cref="NCreature.Position"/>，
    /// 与 NCombatRoom.PositionEnemies（无 scene 战斗的常规布局）坐标系一致；
    /// 有槽位 scene 但槽位占满时也走此路径，槽位标记同样挂在该容器下，口径不变。
    /// </summary>
    private static void PositionStakeWithoutSlot(ICombatState combat, Creature stake)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        NCreature? stakeNode = room?.GetCreatureNode(stake);
        if (room == null || stakeNode == null) return;

        List<NCreature> enemyNodes = combat.Enemies
            .Where(e => e != stake && e.IsAlive)
            .Select(e => room.GetCreatureNode(e))
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();

        if (enemyNodes.Count == 0)
        {
            stakeNode.Position = new Vector2(480f, 200f);
            return;
        }

        NCreature rightmost = enemyNodes.OrderByDescending(n => n.Position.X).First();
        float halfSum = (rightmost.Visuals.Bounds.Size.X + stakeNode.Visuals.Bounds.Size.X) * 0.5f + 60f;
        float x = rightmost.Position.X + halfSum;
        if (x > 940f)
        {
            NCreature leftmost = enemyNodes.OrderBy(n => n.Position.X).First();
            x = leftmost.Position.X - (leftmost.Visuals.Bounds.Size.X + stakeNode.Visuals.Bounds.Size.X) * 0.5f - 60f;
            x = Math.Max(x, 60f);
        }
        stakeNode.Position = new Vector2(x, rightmost.Position.Y);
    }

    /// <summary>
    /// 木桩生命的显示变量，按引擎实际结算路径等价计算：
    /// ① 基础 200（DeniaEtchingStake Min==Max==200，SetUniqueMonsterHpValue 恒取 200，
    ///    同数值怪在场时回退 NextInt(200,201) 仍为 200，无随机浮动）；
    /// ② 多人时 CombatState.CreateCreature → ScaleMonsterHpForMultiplayer：再乘 玩家数×幕系数
    ///    （MultiplayerScalingModel.GetMultiplayerScaling：第1幕 1.1、第2幕 1.2、第3幕 1.2、
    ///    第3幕 Boss 房 1.3；单人=1）；
    /// ③ 升级版由卡牌在生成后按 3/4 比例收缩（SetMaxHpInternal(MaxHp*3/4)，发生在缩放之后）。
    /// 进阶（ToughEnemies）不影响：本体的进阶血量由各怪物类写进 Min/MaxInitialHp，木桩未启用。
    /// 战斗外预览（牌组查看等）无 Encounter，第3幕按非 Boss 的 1.2 计。
    /// </summary>
    private sealed class StakeHpVar(string name, bool assumeUpgraded) : DynamicVar(name, 0m)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            var card = _owner as DeniaEtchedIridescent;
            decimal hp = 200m;

            var run = card?.Owner?.RunState;
            if (run != null && run.Players.Count > 1)
            {
                bool boss = card!.Owner.Creature.CombatState?.Encounter?.RoomType == RoomType.Boss;
                decimal actScale = run.CurrentActIndex switch
                {
                    0 => 1.1m,
                    1 => 1.2m,
                    _ => boss ? 1.3m : 1.2m,
                };
                hp *= run.Players.Count * actScale;
            }

            if (assumeUpgraded || (card?.IsUpgraded ?? false))
            {
                hp = hp * 3m / 4m;
            }

            return hp;
        }
    }

    protected override void OnUpgrade() { }
}
