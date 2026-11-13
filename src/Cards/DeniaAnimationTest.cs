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
        Description: "选择并播放一个待测试动画。仅播放演出，不触发伤害或集谐结算。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        CardModel[] choices = { ModelDb.Card<DeniaExecutionAnimationChoice>().ToMutable() };
        foreach (var choice in choices) choice.Owner = Owner;
        var selected = await CardSelectCmd.FromChooseACardScreen(ctx, choices, Owner);
        if (selected is DeniaExecutionAnimationChoice)
            await DeniaExecutionRule.Play(Owner.Creature.CombatState?.Enemies.FirstOrDefault(enemy => !enemy.IsDead));
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaExecutionAnimationChoice : DeniaTargetChoice
{
    public override int MaxUpgradeLevel => 0;
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_sorrow.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "三级集谐处决",
        Description: "原速播放三级集谐处决的完整画面与音轨。仅测试演出，不触发伤害或集谐结算。");
}
