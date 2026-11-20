using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>内部动画测试入口，仅通过控制台获取，不触发处决结算。</summary>
public sealed class DeniaAnimationTest : DeniaCard
{
    public DeniaAnimationTest() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_sorrow.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "动画测试（内部）",
        Description: "按类别选择一个动画，按当前速度模式完整播放。仅测试演出，不改变形态、伤害或集谐结算。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 原生选牌界面每页最多三个选项，分类逐层选择，不向界面传入超量卡牌。
        CardModel[] choices = [ModelDb.Card<DeniaPinkCategoryAnimationChoice>().ToMutable(),
            ModelDb.Card<DeniaBlackCategoryAnimationChoice>().ToMutable(),
            ModelDb.Card<DeniaEffectsCategoryAnimationChoice>().ToMutable()];
        while (true)
        {
            foreach (var choice in choices) choice.Owner = Owner;
            if (await CardSelectCmd.FromChooseACardScreen(ctx, choices, Owner) is not DeniaAnimationChoice selected) return;
            choices = selected.Choices;
            if (choices.Length > 0) continue;
            if (selected.Preview is not { } kind) return;
            if (kind == DeniaAnimationPreviewKind.Execution)
                await DeniaExecutionRule.Play(Owner.Creature.CombatState?.Enemies.FirstOrDefault(enemy => !enemy.IsDead));
            else
                await DeniaFormPatch.PlayAnimationPreview(Owner.Creature, kind);
            return;
        }
    }

    protected override void OnUpgrade() { }
}

public abstract class DeniaAnimationChoice : DeniaTargetChoice
{
    public override int MaxUpgradeLevel => 0;
    public override string PortraitPath => "res://images/packed/character_select/denia_pink.png";
    internal virtual CardModel[] Choices => [];
    internal virtual DeniaAnimationPreviewKind? Preview => null;
}

public sealed class DeniaPinkCategoryAnimationChoice : DeniaAnimationChoice
{
    internal override CardModel[] Choices => DeniaModConfig.UseCodeAnimation
        ? [ModelDb.Card<DeniaPinkIdleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaStandupCategoryAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkAttackCategoryAnimationChoice>().ToMutable()]
        : [ModelDb.Card<DeniaPinkIdleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkAttackCategoryAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉色形态", Description: "选择要测试的动画。");
}

public sealed class DeniaBlackCategoryAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override CardModel[] Choices => DeniaModConfig.UseCodeAnimation
        ? [ModelDb.Card<DeniaBlackIdleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackRiseAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackAttackCategoryAnimationChoice>().ToMutable()]
        : [ModelDb.Card<DeniaBlackIdleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackAttackCategoryAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑色形态", Description: "选择要测试的动画。");
}

public sealed class DeniaEffectsCategoryAnimationChoice : DeniaAnimationChoice
{
    internal override CardModel[] Choices => [ModelDb.Card<DeniaBubbleCategoryAnimationChoice>().ToMutable(), ModelDb.Card<DeniaExecutionAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "其他演出", Description: "选择要测试的动画。");
}

public sealed class DeniaStandupCategoryAnimationChoice : DeniaAnimationChoice
{
    internal override CardModel[] Choices => [ModelDb.Card<DeniaPinkStandupAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkSitdownAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "起身／坐回", Description: "选择要测试的动画。");
}

public sealed class DeniaPinkAttackCategoryAnimationChoice : DeniaAnimationChoice
{
    internal override CardModel[] Choices => DeniaModConfig.UseCodeAnimation
        ? [ModelDb.Card<DeniaPinkSingleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkMultiAnimationChoice>().ToMutable()]
        : [ModelDb.Card<DeniaPinkSingleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkMultiAnimationChoice>().ToMutable(), ModelDb.Card<DeniaPinkCollapseAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态攻击", Description: "选择要测试的动画。");
}

public sealed class DeniaBlackAttackCategoryAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override CardModel[] Choices => DeniaModConfig.UseCodeAnimation
        ? [ModelDb.Card<DeniaBlackSingleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackMultiAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackFourAnimationChoice>().ToMutable()]
        : [ModelDb.Card<DeniaBlackSingleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackMultiAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackHeavyAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态攻击", Description: "选择要测试的动画。");
}

public sealed class DeniaBlackHeavyAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override CardModel[] Choices => [ModelDb.Card<DeniaBlackTripleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackFourAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态三／四连击", Description: "选择要测试的动画。");
}

public sealed class DeniaBubbleCategoryAnimationChoice : DeniaAnimationChoice
{
    internal override CardModel[] Choices => [ModelDb.Card<DeniaPinkBubbleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaBlackBubbleAnimationChoice>().ToMutable(), ModelDb.Card<DeniaMapBubbleAnimationChoice>().ToMutable()];
    public override List<(string, string)>? Localization => new CardLoc(Title: "泡泡特效", Description: "选择要测试的动画。");
}

public sealed class DeniaPinkIdleAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkIdle;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态待机",
        Description: "原速循环一轮：视频14.500秒，代码10.632秒。测试期间不造成伤害、不改变形态。");
}

public sealed class DeniaPinkStandupAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkStandup;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态起身",
        Description: "正放起身：普通3.083秒，快速1.028秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaPinkSitdownAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkSitdown;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态坐回（倒放）",
        Description: "倒放起身：普通3.083秒，快速1.028秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaPinkSingleAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkSingle;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态单段攻击",
        Description: "视频4.567秒，代码含起身与坐回共11.332秒。快速为对应动作的三倍速。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaPinkMultiAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkMulti;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态多段攻击",
        Description: "视频7.017秒，代码含起身与坐回共20.829秒。快速为对应动作的三倍速。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaBlackIdleAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackIdle;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态待机",
        Description: "原速循环一轮：视频2.983秒，代码5.316秒。测试期间不造成伤害、不改变形态。");
}

public sealed class DeniaBlackRiseAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackRise;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态起身（备用）",
        Description: "备用演出，所有速度下均为3秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaBlackSingleAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackSingle;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态单段攻击",
        Description: "视频9.783秒，代码7.933秒。快速为对应动作的三倍速。测试期间不造成伤害、不改变形态。");
}

public sealed class DeniaBlackMultiAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackMulti;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态二／三段攻击",
        Description: "视频模式预览二连击；代码模式下二、三段共用此动作。视频8.167秒，代码8.783秒。快速为对应动作的三倍速。测试期间不造成伤害、不改变形态。");
}

public sealed class DeniaBlackTripleAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackTriple;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态三段攻击",
        Description: "黑态专用三连击视频。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaPinkCollapseAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkCollapse;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态法杖收束",
        Description: "预览粉态法杖收束视频。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaBlackFourAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackFour;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态四段以上攻击",
        Description: "视频11.917秒，代码11.466秒。快速为对应动作的三倍速。测试期间不造成伤害、不改变形态。");
}

public sealed class DeniaPinkBubbleAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.PinkBubble;
    public override List<(string, string)>? Localization => new CardLoc(Title: "粉态点击泡泡",
        Description: "原速播放一组点击泡泡，最多1.5秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaBlackBubbleAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/character_select/denia_black.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.BlackBubble;
    public override List<(string, string)>? Localization => new CardLoc(Title: "黑态点击泡泡",
        Description: "原速播放一组点击泡泡，最多1.5秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaMapBubbleAnimationChoice : DeniaAnimationChoice
{
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.MapBubble;
    public override List<(string, string)>? Localization => new CardLoc(Title: "地图落笔泡泡",
        Description: "地图落笔的慢速粉态泡泡，最多2.727秒。测试期间等待完整演出，不造成伤害、不改变形态。");
}

public sealed class DeniaExecutionAnimationChoice : DeniaAnimationChoice
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_sorrow.png";
    internal override DeniaAnimationPreviewKind? Preview => DeniaAnimationPreviewKind.Execution;
    public override List<(string, string)>? Localization => new CardLoc(Title: "三级集谐处决",
        Description: "原速播放完整画面与音轨。必须等画面完整结束（约6.133秒），不可被下一演出打断，尾音继续。不造成伤害、不改变形态。");
}
