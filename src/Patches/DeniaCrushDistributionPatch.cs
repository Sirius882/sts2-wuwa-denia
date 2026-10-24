using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using AemeathWw.Scripts.Api;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>
/// 轧碎的聚爆引爆伤害集中规则 + 木桩的谐度破坏易伤修正，注册进 Aemeath 公开规则入口。
/// </summary>
public static class DeniaCrushDistributionPatch
{
    private static readonly IAemeathAutoBurstDamageDistributionRule Rule = new DeniaCrushDistributionRule();
    private static bool _registered;

    public static void Init()
    {
        if (_registered)
            return;
        _registered = true;

        AemeathFusionBurstRules.RegisterAutoBurstDamageDistributionRule(Rule);

        // 木桩的谐度破坏易伤：Aemeath 以 MaxHp×percent 结算，翻倍 percent 即翻倍最终伤害。
        AemeathMechanicsApi.RegisterResonanceBreakDamagePercentModifier(ModifyStakeRupturePercent);
    }

    private static decimal ModifyStakeRupturePercent(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature applier,
        CardModel source,
        bool isUnconditional,
        decimal percent) =>
        target.GetPower<DeniaEtchingStakePower>() != null ? percent * 2m : percent;

    private sealed class DeniaCrushDistributionRule : IAemeathAutoBurstDamageDistributionRule
    {
        public int Priority => 0;

        public bool ShouldTakeOver(AemeathAutoBurstDamageDistributionContext context) =>
            ResolveOwner(context)?.GetPower<DeniaCrushPower>() != null;

        public async Task ApplyDistribution(AemeathAutoBurstDamageDistributionContext context)
        {
            // 逐目标用与默认结算一致的改伤链预结算份额（含各自易伤/增伤倍率；不含格挡），
            // 求和后集中落到被引爆的目标。敌方只有1个目标时按设计按2份计算。
            IRunState runState = IRunState.GetFrom(context.Enemies.Append(context.Target));
            ICombatState? combatState = context.Target.CombatState;

            decimal total = 0m;
            foreach (Creature enemy in context.Enemies)
            {
                total += Hook.ModifyDamage(
                    runState,
                    combatState,
                    enemy,
                    context.Applier,
                    context.Damage,
                    ValueProp.Unpowered,
                    context.Source,
                    null,
                    ModifyDamageHookType.All,
                    CardPreviewMode.None,
                    out _);
            }

            if (context.Enemies.Count == 1)
            {
                total *= 2m;
            }

            if (total <= 0m)
            {
                return;
            }

            // 走集中结算入口：落地时不再重复套用“仅引爆伤害生效”的易伤倍率。
            await AemeathFusionBurstState.DealConcentratedDetonationDamage(
                context.Target, total, context.Applier, context.Source);
        }

        // “你的聚爆伤害”：以附加聚爆的玩家为准（applier 优先，其次卡牌来源持有者）。
        private static Creature? ResolveOwner(AemeathAutoBurstDamageDistributionContext context) =>
            context.Applier is { IsPlayer: true } applier
                ? applier
                : context.Source?.Owner?.Creature;
    }
}
