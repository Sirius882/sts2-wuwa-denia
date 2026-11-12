using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace Denia;

/// <summary>
/// Denia 自定义怪物公共基类。走 CreateCustomVisuals 自建立绘的怪物不提供 .tscn，
/// 覆写 AssetPaths 去掉引擎无条件预加载的 VisualsPath，只保留各招式意图自身的资源
/// （BaseLib CustomMonsterModel 的注释同样要求自建视觉时覆写此处，否则预加载报错）。
/// </summary>
public abstract class DeniaMonsterModel : CustomMonsterModel
{
    public override IEnumerable<string> AssetPaths
    {
        get
        {
            List<string> assetPaths = [];
            foreach (AbstractIntent intent in CollectIntents())
            {
                assetPaths.AddRange(intent.AssetPaths);
            }

            return assetPaths;
        }
    }

    // 等价复刻原版 MonsterModel.GetIntents()（private）：只收集 move 状态上的意图。
    private List<AbstractIntent> CollectIntents()
    {
        List<AbstractIntent> intents = [];
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state.IsMove && state is MoveState moveState)
            {
                intents.AddRange(moveState.Intents);
            }
        }

        return intents;
    }
}
