using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

// 选项沿用原生同步选卡流程，不加入奖励池或图鉴。
public abstract class DeniaTargetChoice : DeniaCard
{
    protected DeniaTargetChoice() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() { }

    public static async Task<bool> ChooseAll<TSingle, TAll>(PlayerChoiceContext ctx, CardModel source)
        where TSingle : CardModel where TAll : CardModel
    {
        CardModel[] choices = { ModelDb.Card<TSingle>().ToMutable(), ModelDb.Card<TAll>().ToMutable() };
        foreach (var choice in choices)
        {
            choice.Owner = source.Owner;
            if (source.IsUpgraded) CardCmd.Upgrade(choice);
        }
        return await CardSelectCmd.FromChooseACardScreen(ctx, choices, source.Owner) is TAll;
    }
}

public sealed class DeniaWeakSingleChoice : DeniaTargetChoice
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_long_time_no_see.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "单体虚弱",
        Description: "给予目标{IfUpgraded:show:5|3}层虚弱。");
}

public sealed class DeniaWeakAllChoice : DeniaTargetChoice
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_long_time_no_see.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "全体虚弱",
        Description: "给予所有敌人{IfUpgraded:show:3|2}层虚弱。");
}

public sealed class DeniaStrengthSingleChoice : DeniaTargetChoice
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_knock_door.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "单体失去力量",
        Description: "令目标失去{IfUpgraded:show:8|6}点力量。持续1回合。");
}

public sealed class DeniaStrengthAllChoice : DeniaTargetChoice
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_knock_door.png";
    public override List<(string, string)>? Localization => new CardLoc(Title: "全体失去力量",
        Description: "令所有敌人失去{IfUpgraded:show:6|4}点力量。持续1回合。");
}
