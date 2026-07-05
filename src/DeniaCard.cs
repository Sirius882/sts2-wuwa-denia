using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Players;
using TuneStrain;
using TuneStrain.Powers;

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

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            foreach (IHoverTip tip in base.ExtraHoverTips)
                yield return tip;

            foreach (IHoverTip tip in GetDeniaMechanicHoverTips())
                yield return tip;
        }
    }

    private IEnumerable<IHoverTip> GetDeniaMechanicHoverTips()
    {
        string description = GetDescriptionTextForHoverScan();

        if (CurrentVirtualMatterCost > 0)
            yield return new HoverTip(
                new LocString("card_keywords", "DENIA-VIRTUAL_MATTER_ENHANCEMENT.title"),
                new LocString("card_keywords", "DENIA-VIRTUAL_MATTER_ENHANCEMENT.description"));

        if (CurrentDarkCoreCost > 0)
            yield return new HoverTip(
                new LocString("card_keywords", "DENIA-DARK_CORE_ENHANCEMENT.title"),
                new LocString("card_keywords", "DENIA-DARK_CORE_ENHANCEMENT.description"));

        if (Mentions(description, "虚质", "Virtual Matter"))
            yield return DeniaKeywordTip("VIRTUAL_MATTER");

        if (Mentions(description, "黯核", "Dark Core"))
            yield return DeniaKeywordTip("DARK_CORE");

        if (Mentions(description, "粉色形态", "Pink Form"))
            yield return DeniaKeywordTip("PINK_FORM");

        if (Mentions(description, "黑色形态", "Black Form"))
            yield return DeniaKeywordTip("BLACK_FORM");

        if (Mentions(description, "集谐·偏移", "Tune Strain Bias", "Tune Strain · Bias"))
            yield return HoverTipFactory.FromPower<TuneStrainBiasPower>();

        if (Mentions(description, "集谐·干涉", "Tune Strain Interference", "Tune Strain · Interference"))
            yield return HoverTipFactory.FromPower<TuneStrainInterferencePower>();

        if (Mentions(description, "集谐响应", "Tune Strain Response"))
        {
            if (TuneStrainKeywords.TuneStrainResponse != CardKeyword.None)
                yield return HoverTipFactory.FromKeyword(TuneStrainKeywords.TuneStrainResponse);
            yield return HoverTipFactory.FromPower<TuneStrainResponsePower>();
        }

        if (Mentions(description, "偏谐", "Off-Tune", "Off Tune"))
            yield return HoverTipFactory.FromPower<AemeathDetunePower>();

        if (Mentions(description, "失谐", "Dissonance"))
            yield return HoverTipFactory.FromPower<AemeathDissonancePower>();

        if (MentionsAfterRemoving(description,
            ["集谐·干涉", "Tune Strain Interference", "Tune Strain · Interference"],
            "干涉", "Interference"))
            yield return HoverTipFactory.FromPower<AemeathInterferencePower>();

        if (Mentions(description, "谐度破坏", "Resonance Break") && AemeathSpecialKeywords.ResonanceBreak != CardKeyword.None)
            yield return HoverTipFactory.FromKeyword(AemeathSpecialKeywords.ResonanceBreak);

        if (Mentions(description, "熔解", "Melt") && AemeathSpecialKeywords.Melt != CardKeyword.None)
            yield return HoverTipFactory.FromKeyword(AemeathSpecialKeywords.Melt);

        if (Mentions(description, "聚爆轨迹", "Fusion Burst Trajectory"))
            yield return HoverTipFactory.FromPower<AemeathFusionBurstTrajectoryPower>();

        if (Mentions(description, "聚爆上限", "Fusion Burst Cap"))
            yield return HoverTipFactory.FromPower<AemeathFusionBurstCapPower>();

        if (MentionsAfterRemoving(description,
            ["聚爆轨迹", "聚爆上限", "Fusion Burst Trajectory", "Fusion Burst Cap"],
            "聚爆", "Fusion Burst"))
            yield return HoverTipFactory.FromPower<AemeathFusionBurstPower>();

        if (Mentions(description, "引爆", "detonation", "Auto-Burst") && AemeathSpecialKeywords.AutoBurst != CardKeyword.None)
            yield return HoverTipFactory.FromKeyword(AemeathSpecialKeywords.AutoBurst);
    }

    private string GetDescriptionTextForHoverScan()
    {
        try
        {
            return Description.GetRawText();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool Mentions(string text, params string[] terms)
    {
        foreach (string term in terms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IHoverTip DeniaKeywordTip(string key) => new HoverTip(
        new LocString("card_keywords", $"DENIA-{key}.title"),
        new LocString("card_keywords", $"DENIA-{key}.description"));

    private static bool MentionsAfterRemoving(string text, string[] excludedTerms, params string[] terms)
    {
        foreach (string excludedTerm in excludedTerms)
            text = text.Replace(excludedTerm, string.Empty, StringComparison.OrdinalIgnoreCase);

        return Mentions(text, terms);
    }

    protected bool TryGetOwner(out Player? owner)
    {
        owner = null;
        if (!IsMutable) return false;

        owner = Owner;
        return owner != null;
    }

    /// <summary>尝试消耗虚质。仅在[gold]黑色形态[/gold]且虚质足够时返回 true 并实际扣除。</summary>
    protected async Task<bool> TrySpendVirtualMatter(CardPlay cardPlay)
    {
        if (CurrentVirtualMatterCost <= 0) return false;
        if (cardPlay.IsAutoPlay) return false;
        return await DeniaResourceState.TrySpendVirtualMatter(
            Owner.Creature, CurrentVirtualMatterCost, Owner.Creature, this);
    }

    /// <summary>尝试消耗黯核。仅在[gold]黑色形态[/gold]且黯核足够时返回 true 并实际扣除。</summary>
    protected async Task<bool> TrySpendDarkCore(CardPlay cardPlay)
    {
        if (CurrentDarkCoreCost <= 0) return false;
        if (cardPlay.IsAutoPlay) return false;
        return await DeniaResourceState.TrySpendDarkCore(
            Owner.Creature, CurrentDarkCoreCost, Owner.Creature, this);
    }
}
