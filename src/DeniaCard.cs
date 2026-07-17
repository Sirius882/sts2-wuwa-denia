using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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

        if (Mentions(description, "聚爆上限", "Fusion Burst Cap"))
            yield return HoverTipFactory.FromPower<AemeathFusionBurstCapPower>();

        if (MentionsAfterRemoving(description,
            ["聚爆上限", "Fusion Burst Cap"],
            "聚爆", "Fusion Burst"))
            yield return HoverTipFactory.FromPower<AemeathFusionBurstPower>();

        if (Mentions(description, "引爆", "detonation", "Auto-Burst") && AemeathSpecialKeywords.AutoBurst != CardKeyword.None)
            yield return HoverTipFactory.FromKeyword(AemeathSpecialKeywords.AutoBurst);

        if (Mentions(description, "冻伤", "Frostbite"))
            yield return HoverTipFactory.FromPower<DeniaFrostbitePower>();

        if (Mentions(description, "蔽星", "Shrouded Star"))
            yield return DeniaKeywordTip("SHROUDED_STAR");
    }

    /// <summary>
    /// 为关键词扫描准备“当前升级态可见”的描述文本。
    /// 不能裸调 Description.GetFormattedText()：hover 时 variables 为空，
    /// {Damage}/{Block} 等会抛 Localization formatting error 并刷屏。
    /// 应像 CardModel.GetDescriptionForPile 一样先注入 DynamicVars + IfUpgraded。
    /// </summary>
    private string GetDescriptionTextForHoverScan()
    {
        try
        {
            LocString description = Description;
            // 注入卡牌动态变量（Damage/Block/IfUpgraded 等）
            DynamicVars.AddTo(description);
            description.Add(new IfUpgradedVar(IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));
            return description.GetFormattedText();
        }
        catch (Exception)
        {
            // 回退：只解析 IfUpgraded 分支，忽略 Damage/Block 等数值占位符
            try
            {
                return ResolveIfUpgradedForScan(Description.GetRawText(), IsUpgraded);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// 轻量解析 {IfUpgraded:show:升级文案|基础文案}，用于 GetFormattedText 失败时的扫描回退。
    /// </summary>
    private static string ResolveIfUpgradedForScan(string raw, bool isUpgraded)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        // {IfUpgraded:show:A|B} 或 {IfUpgraded:show:A|}
        return Regex.Replace(
            raw,
            @"\{IfUpgraded:show:([^|}]*)\|?([^}]*)\}",
            m => isUpgraded ? m.Groups[1].Value : m.Groups[2].Value,
            RegexOptions.CultureInvariant);
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
        bool spent = await DeniaResourceState.TrySpendVirtualMatter(
            Owner.Creature, CurrentVirtualMatterCost, Owner.Creature, this);
        if (spent)
            await DeniaEnhancementEvents.NotifyVirtualMatterEnhanced(Owner.Creature);
        return spent;
    }

    /// <summary>尝试消耗黯核。仅在[gold]黑色形态[/gold]且黯核足够时返回 true 并实际扣除。</summary>
    protected async Task<bool> TrySpendDarkCore(CardPlay cardPlay)
    {
        if (CurrentDarkCoreCost <= 0) return false;
        if (cardPlay.IsAutoPlay) return false;
        bool spent = await DeniaResourceState.TrySpendDarkCore(
            Owner.Creature, CurrentDarkCoreCost, Owner.Creature, this);
        if (spent)
            await DeniaEnhancementEvents.NotifyDarkCoreEnhanced(Owner.Creature);
        return spent;
    }
}

/// <summary>虚质/黯核强化成功后的全局通知，供「幻沫」「向虚而行」等监听。</summary>
public static class DeniaEnhancementEvents
{
    public static async Task NotifyVirtualMatterEnhanced(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature)
    {
        await DeniaPhantomFoamPower.OnVirtualMatterEnhanced(creature);
    }

    public static async Task NotifyDarkCoreEnhanced(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature)
    {
        await DeniaTowardVoidPower.OnDarkCoreEnhanced(creature);
    }
}

