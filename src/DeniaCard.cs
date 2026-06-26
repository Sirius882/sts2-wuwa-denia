using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Denia;

/// <summary>
/// 达妮娅卡牌基类。提供虚质/黯核能量消耗的公共判定逻辑。
/// 能量不足不阻挡打出，仅跳过强化效果。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public abstract class DeniaCard(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true,
    bool autoAdd = true
) : CustomCardModel(baseCost, type, rarity, target, showInCardLibrary, autoAdd)
{
    /// <summary>虚质图标 BBcode，用于牌面描述。</summary>
    public const string IconVm = "[img]res://images/ui/combat/denia_virtual_matter_cost_icon.png[/img]";
    /// <summary>黯核图标 BBcode，用于牌面描述。</summary>
    public const string IconDc = "[img]res://images/ui/combat/denia_dark_core_cost_icon.png[/img]";

    /// <summary>虚质消耗量（用于卡牌左上角图标显示 & 消耗判定）。0 = 无虚质强化。</summary>
    public virtual int CurrentVirtualMatterCost => 0;

    /// <summary>黯核消耗量（用于卡牌左上角图标显示 & 消耗判定）。0 = 无黯核强化。</summary>
    public virtual int CurrentDarkCoreCost => 0;

    /// <summary>尝试消耗虚质。仅在黑色形态且虚质足够时返回 true 并实际扣除。</summary>
    protected async Task<bool> TrySpendVirtualMatter(CardPlay cardPlay)
    {
        if (CurrentVirtualMatterCost <= 0) return false;
        if (cardPlay.IsAutoPlay) return false;
        return await DeniaResourceState.TrySpendVirtualMatter(
            Owner.Creature, CurrentVirtualMatterCost, Owner.Creature, this);
    }

    /// <summary>尝试消耗黯核。仅在黑色形态且黯核足够时返回 true 并实际扣除。</summary>
    protected async Task<bool> TrySpendDarkCore(CardPlay cardPlay)
    {
        if (CurrentDarkCoreCost <= 0) return false;
        if (cardPlay.IsAutoPlay) return false;
        return await DeniaResourceState.TrySpendDarkCore(
            Owner.Creature, CurrentDarkCoreCost, Owner.Creature, this);
    }
}
