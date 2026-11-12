using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 蚀刻繁彩的回血载体（挂在打出者身上的隐藏 power）：木桩被玩家击杀的当场，
/// 全体非死亡玩家立即恢复 <see cref="HealPerStake"/> 点生命。
///
/// 前两版（木桩 power 上做胜利标记 / 玩家 power 上等 AfterCombatVictory 结算）
/// 实测回血均未触发，改为在击杀伤害事件（AfterDamageGiven）里当场结算，
/// 不再依赖任何战斗结束钩子。爪牙清扫处死不经过伤害事件，天然不会触发，
/// 保持设计稿"被玩家打死而非死于爪牙效果"的口径。
/// </summary>
public sealed class DeniaEtchingStakeHealPower : CustomPowerModel
{
    /// <summary>每根木桩被玩家击杀时的单人回血量（多次打出时 Single 叠层只合并 Amount，单价以最新一发为准）。</summary>
    internal decimal HealPerStake;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    // 隐藏 power：不显示图标与层数，仅承载结算逻辑（可见的数值展示在木桩的 DeniaEtchingStakePower 描述里）。
    protected override bool IsVisibleInternal => false;

    public override bool ShouldPlayVfx => false;

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (target.Monster is DeniaEtchingStake
            && result.WasTargetKilled
            && dealer?.Side == CombatSide.Player)
        {
            foreach (var player in Owner.CombatState?.Players ?? [])
            {
                if (!player.Creature.IsDead)
                {
                    await CreatureCmd.Heal(player.Creature, HealPerStake);
                }
            }
        }
    }
}
