using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaResonanceMode : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_resonance_mode.png";

    public DeniaResonanceMode()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "共鸣模态·集谐",
        Description: "进入[gold]共鸣模态·集谐[/gold]，给任意两张手牌附加[gold]集谐响应[/gold]。\n黯核强化：选择范围扩大到整个持有的牌组，可选4张牌。\n[gold]共鸣模态·集谐·达妮娅[/gold]：计算集谐增伤时，按1.5倍采用你的集谐响应 power 层数（向下取整）。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        bool darkCoreEnhanced = await TrySpendDarkCore(play);

        await PowerCmd.Apply<DeniaResonanceModePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
        await DeniaFormHelper.MarkResonanceModePermanent(Owner.Creature);

        IEnumerable<CardModel> selected;
        if (darkCoreEnhanced)
        {
            int count = 4;
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
            selected = await CardSelectCmd.FromDeckGeneric(
                Owner,
                prefs,
                card => card != DeckVersion
                    && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
                    && !TuneStrainState.HasTemporaryResponse(card));
        }
        else
        {
            int count = 2;
            var eligible = PileType.Hand.GetPile(Owner).Cards
                .Where(card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse))
                .ToList();
            count = System.Math.Min(count, eligible.Count);
            if (count <= 0) return;

            var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
            selected = await CardSelectCmd.FromHand(ctx, Owner, prefs,
                card => card != this && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse), this);
        }

        foreach (var card in selected.ToList())
            TuneStrainState.AddTemporaryResponse(Owner, card);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 共鸣模态·集谐（达妮娅专属）：可见 buff 标记。
/// 激活时让集谐系统在计算集谐响应度时按 1.5 倍采用响应 power 层数（向下取整）。
/// 通过在静态构造里向 TuneStrainState.RegisterResponseDegreeMultiplier 注册一个回调实现解耦。
/// </summary>
public sealed class DeniaResonanceModePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(Title: "共鸣模态·集谐",
            Description: "计算集谐增伤时，按1.5倍采用你的集谐响应 power 层数（向下取整）。",
            SmartDescription: "计算集谐增伤时，按1.5倍采用你的集谐响应 power 层数（向下取整）。");

    static DeniaResonanceModePower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaResonanceModePower));
        // 注册一次：回调在每次计算响应度时被询问；返回 1.5 表示本玩家处于共鸣模态则按 1.5 倍采用层数（GetResponseDegree 内再向下取整）。
        TuneStrainState.RegisterResponseDegreeMultiplier(creature =>
            creature.GetPower<DeniaResonanceModePower>() != null ? 1.5 : 1.0);
    }
}

public static class DeniaResonanceModeHelper
{
    public static bool IsActive(Creature? creature) =>
        creature?.GetPower<DeniaResonanceModePower>() != null;
}
