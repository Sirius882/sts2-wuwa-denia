#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>
/// 得到太阳 — Uncommon Power.
/// 每附加 3 聚爆，给牌组中随机一张牌附加临时集谐响应。
/// 升级：固有。虚质强化：恢复 1 能量。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGetSun : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_get_sun.png";

    public DeniaGetSun()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "得到太阳",
            Description: "每附加3[color=#9A6A18]聚爆[/color]，给牌组中随机一张牌附加[color=#9A6A18]集谐响应[/color]。不可叠加。\n虚质强化：恢复1能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaGetSunPower>() != null) return;

        // 进度余数使用独立隐藏 power，Amount = 余数+1，避免归零移除或在回调枚举中增删 power。
        await PowerCmd.Apply<DeniaGetSunProgressPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        await PowerCmd.Apply<DeniaGetSunPower>(ctx, Owner.Creature, 1, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
            await PlayerCmd.GainEnergy(1m, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 隐藏计数器 Amount = 已累计但未触发的聚爆层数+1 (1–3)。
/// 通过 IOnFusionBurstAppliedPower 监听“自己成功附加聚爆”。
/// </summary>
public sealed class DeniaGetSunPower : CustomPowerModel, IOnFusionBurstAppliedPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    // 无独立美术：借用共鸣模态·集谐图标
    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "得到太阳",
        Description: "每附加3聚爆，给牌组中随机一张尚无集谐响应的牌附加临时集谐响应。若所有牌都已拥有，则跳过。不可叠加。",
        SmartDescription: "每附加3聚爆，给牌组中随机一张尚无集谐响应的牌附加临时集谐响应。若所有牌都已拥有，则跳过。不可叠加。");

    public async Task OnFusionBurstApplied(Creature target, int amount)
    {
        if (Owner == null || Owner.IsDead || amount <= 0) return;

        if (DeniaBurstFillGuard.IsActive) return;
        var counter = Owner.GetPower<DeniaGetSunProgressPower>();
        if (counter == null) return;
        int progress = counter.Amount - 1 + amount;
        int triggers = progress / 3;
        int remainder = progress % 3;
        int delta = remainder + 1 - counter.Amount;
        if (delta != 0)
            await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(), counter, delta, Owner, null);

        if (triggers <= 0) return;

        Flash();
        var player = Owner.Player;
        if (player == null) return;

        for (int i = 0; i < triggers; i++)
        {
            // 与骗术师/赝作矮星同款：只在无集谐响应的牌里随机；全满则跳过
            var candidates = player.Deck.Cards
                .Where(c => !c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
                            && !TuneStrainState.HasTemporaryResponse(c))
                .ToList();
            if (candidates.Count == 0)
                continue;

            var pick = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
            if (pick == null) continue;
            TuneStrainState.AddTemporaryResponse(player, pick);
        }
    }
}
