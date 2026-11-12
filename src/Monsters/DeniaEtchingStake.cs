using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

/// <summary>
/// 蚀刻繁彩的木桩：无行动站桩怪，只作为“被玩家击杀后回血”的靶子。
/// 基础 200 血；升级版由卡牌在生成后按比例收缩到 150。爪牙标记（MinionPower）由卡牌附加：
/// 不阻止战斗结束，主敌全灭时随清扫处死（不算玩家击杀，不给治疗）。
/// 易伤与击杀结算见 DeniaEtchingStakePower。
/// </summary>
public sealed class DeniaEtchingStake : DeniaMonsterModel
{
    public override int MinInitialHp => 200;

    public override int MaxInitialHp => MinInitialHp;

    public override NCreatureVisuals? CreateCustomVisuals() =>
        DeniaMonsterVisuals.Create("res://images/monsters/denia_etching_stake.png", 300f);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState idle = new("IDLE", IdleMove, new HiddenIntent());
        idle.FollowUpState = idle;
        return new MonsterMoveStateMachine([idle], idle);
    }

    private static Task IdleMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
