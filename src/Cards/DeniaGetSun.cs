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

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "得到太阳",
        Description: "每附加3[gold]聚爆[/gold]，给牌组中随机一张牌附加[gold]集谐响应[/gold]。\n虚质强化：恢复1能量。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // Amount 展示进度余数；初始 0 不能 Apply，先 Apply 1 再改回 0
        await PowerCmd.Apply<DeniaGetSunPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
        var pwr = Owner.Creature.GetPower<DeniaGetSunPower>();
        if (pwr != null && pwr.Amount != 0)
            await PowerCmd.ModifyAmount(ctx, pwr, -pwr.Amount, Owner.Creature, this);

        if (await TrySpendVirtualMatter(play))
            await PlayerCmd.GainEnergy(1m, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// Amount = 已累计但未触发的聚爆层数 (0–2)。
/// 通过 IOnFusionBurstAppliedPower 监听“自己成功附加聚爆”。
/// </summary>
public sealed class DeniaGetSunPower : CustomPowerModel, IOnFusionBurstAppliedPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    // 无独立美术：借用共鸣模态·集谐图标
    public override string? CustomPackedIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";
    public override string? CustomBigIconPath =>
        "res://images/ui/powers/denia_resonance_mode_tune_strain_power.png";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "得到太阳",
        Description: "每附加3聚爆，给牌组中随机一张牌附加集谐响应。进度：{Amount}/3。",
        SmartDescription: "每附加3聚爆，给牌组中随机一张牌附加集谐响应。进度：{Amount}/3。");

    public async Task OnFusionBurstApplied(Creature target, int amount)
    {
        if (Owner == null || Owner.IsDead || amount <= 0) return;

        int progress = Amount + amount;
        int triggers = progress / 3;
        int remainder = progress % 3;
        int delta = remainder - Amount;
        if (delta != 0)
            await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(), this, delta, Owner, null);

        if (triggers <= 0) return;

        Flash();
        var player = Owner.Player;
        if (player == null) return;

        for (int i = 0; i < triggers; i++)
        {
            var candidates = player.Deck.Cards
                .Where(c => !c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
                            && !TuneStrainState.HasTemporaryResponse(c))
                .ToList();
            if (candidates.Count == 0)
            {
                candidates = player.Deck.Cards.ToList();
                if (candidates.Count == 0) return;
            }

            var pick = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
            if (pick == null) continue;
            TuneStrainState.AddTemporaryResponse(player, pick);
        }
    }
}
