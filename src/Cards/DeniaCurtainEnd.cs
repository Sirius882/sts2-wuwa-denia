#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using TuneStrain;

namespace Denia;

/// <summary>
/// 帷幕终景 — Uncommon Skill, 0e, Exhaust+Retain.
/// 选择一张手牌，将复制品放入抽牌堆。
/// 未升级：主动给复制品加 消耗、虚无。
/// 升级：不再主动添加；若源牌自带则保留（升级只是消极地不添加）。
/// 源牌临时集谐响应会复制到复制品。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaCurtainEnd : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, CardKeyword.Retain };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_curtain_end.png";

    public DeniaCurtainEnd()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "帷幕终景",
            Description: "选择一张手牌，将一张{IfUpgraded:show:升级过的|}复制品放入[color=#9A6A18]抽牌堆[/color]。{IfUpgraded:show:|复制品获得[color=#9A6A18]消耗[/color]、[color=#9A6A18]虚无[/color]。}");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToList();
        if (hand.Count == 0) return;

        var prefs = new CardSelectorPrefs(
            new LocString("gameplay_ui", "CHOOSE_CARD_UPGRADE_HEADER"), 1);
        var selected = await CardSelectCmd.FromHand(ctx, Owner, prefs, c => c != this, this);
        var pick = selected?.FirstOrDefault();
        if (pick == null) return;

        // CreateClone 保留源牌当前 Keywords（含自带消耗/虚无、已升级状态）
        var dupe = pick.CreateClone();
        if (dupe == null) return;

        // 升级后：放入“升级过的复制品”；不再主动添加消耗/虚无
        if (IsUpgraded && dupe.IsUpgradable && !dupe.IsUpgraded)
            CardCmd.Upgrade(dupe);

        // 未升级：主动给复制品加 Exhaust+Ethereal
        if (!IsUpgraded)
        {
            if (!dupe.Keywords.Contains(CardKeyword.Exhaust))
                dupe.AddKeyword(CardKeyword.Exhaust);
            if (!dupe.Keywords.Contains(CardKeyword.Ethereal))
                dupe.AddKeyword(CardKeyword.Ethereal);
        }

        await CardPileCmd.AddGeneratedCardToCombat(dupe, PileType.Draw, Owner);

        // 源牌临时集谐响应 → 复制品也带
        if (TuneStrainState.HasTemporaryResponse(pick)
            || pick.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse))
        {
            // 若源是临时响应，复制品也应是临时（战斗结束清除）
            if (TuneStrainState.HasTemporaryResponse(pick)
                || !pick.CanonicalKeywords.Contains(TuneStrainKeywords.TuneStrainResponse))
            {
                TuneStrainState.AddTemporaryResponse(Owner, dupe);
            }
            else if (!dupe.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse))
            {
                dupe.AddKeyword(TuneStrainKeywords.TuneStrainResponse);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果体现在 OnPlay 不再主动加 Exhaust/Ethereal，无数值改动
    }
}
