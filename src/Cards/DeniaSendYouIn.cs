#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>
/// 送你进去 — Uncommon Skill.
/// 夺取目标白名单正面 buff：等量给自己，并令目标失去；流电/活力火花只令对方失去。
/// 难以杀灭：层数越少越好，多次夺取取较小层数（不叠加）。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSendYouIn : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_send_you_in.png";

    public DeniaSendYouIn()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "送你进去",
            Description: "夺取目标的正面[color=#9A6A18]buff[/color]。");

    // 可夺取（自己获得）
    private static readonly Type[] StealAndGain =
    {
        typeof(StrengthPower),
        typeof(DexterityPower),
        typeof(IntangiblePower),
        typeof(SlipperyPower),
        typeof(ArtifactPower),
        typeof(PlatingPower),
        typeof(RitualPower),
        typeof(HardToKillPower),
        typeof(ThornsPower),
        typeof(HardenedShellPower),
        typeof(FlutterPower),
        typeof(PaperCutsPower),
    };

    // 只令对方失去
    private static readonly Type[] RemoveOnly =
    {
        typeof(GalvanicPower),
        typeof(VitalSparkPower),
    };

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var target = play.Target;
        var self = Owner.Creature;

        // 先快照层数，再移除/施加，避免遍历时集合变化
        var stealSnapshots = new List<(Type type, int amount)>();
        foreach (var t in StealAndGain)
        {
            var p = target.Powers.FirstOrDefault(x => t.IsInstanceOfType(x));
            // 只夺取正层数 buff；负力量/敏捷属于 debuff，不在白名单意图内
            if (p == null || p.Amount <= 0) continue;
            if (p.TypeForCurrentAmount != PowerType.Buff)
                continue;
            stealSnapshots.Add((t, p.Amount));
        }

        var removeSnapshots = new List<PowerModel>();
        foreach (var t in RemoveOnly)
        {
            var p = target.Powers.FirstOrDefault(x => t.IsInstanceOfType(x));
            if (p == null) continue;
            removeSnapshots.Add(p);
        }

        // 移除目标可夺取 buff，并等量给自己
        foreach (var (type, amount) in stealSnapshots)
        {
            var existing = target.Powers.FirstOrDefault(x => type.IsInstanceOfType(x));
            if (existing != null)
                await PowerCmd.Remove(existing);

            // 用永久 power 施加到自己（无限持续）
            await ApplyByType(ctx, type, self, amount);
        }

        // 只移除
        foreach (var p in removeSnapshots)
            await PowerCmd.Remove(p);
    }

    private async Task ApplyByType(PlayerChoiceContext ctx, Type type, Creature self, int amount)
    {
        if (amount <= 0) return;

        // 难以杀灭：Amount = 每击伤害上限，层数越少越好。多次夺取取 min，不 Counter 叠加。
        if (type == typeof(HardToKillPower))
        {
            await ApplyHardToKillMin(ctx, self, amount);
            return;
        }

        // 按类型分发到泛型 Apply，保证走引擎正规施加路径
        if (type == typeof(StrengthPower))
            await PowerCmd.Apply<StrengthPower>(ctx, self, amount, self, this);
        else if (type == typeof(DexterityPower))
            await PowerCmd.Apply<DexterityPower>(ctx, self, amount, self, this);
        else if (type == typeof(IntangiblePower))
            await PowerCmd.Apply<IntangiblePower>(ctx, self, amount, self, this);
        else if (type == typeof(SlipperyPower))
            await PowerCmd.Apply<SlipperyPower>(ctx, self, amount, self, this);
        else if (type == typeof(ArtifactPower))
            await PowerCmd.Apply<ArtifactPower>(ctx, self, amount, self, this);
        else if (type == typeof(PlatingPower))
            await PowerCmd.Apply<PlatingPower>(ctx, self, amount, self, this);
        else if (type == typeof(RitualPower))
            await PowerCmd.Apply<RitualPower>(ctx, self, amount, self, this);
        else if (type == typeof(ThornsPower))
            await PowerCmd.Apply<ThornsPower>(ctx, self, amount, self, this);
        else if (type == typeof(HardenedShellPower))
            await PowerCmd.Apply<HardenedShellPower>(ctx, self, amount, self, this);
        else if (type == typeof(FlutterPower))
            await PowerCmd.Apply<FlutterPower>(ctx, self, amount, self, this);
        else if (type == typeof(PaperCutsPower))
            await PowerCmd.Apply<PaperCutsPower>(ctx, self, amount, self, this);
    }

    /// <summary>
    /// 自己尚无难以杀灭 → 施加夺来的层数；
    /// 已有 → 保留较小者（更强）：若已有 ≤ 夺来则不动，否则 ModifyAmount 降到夺来层数。
    /// </summary>
    private async Task ApplyHardToKillMin(PlayerChoiceContext ctx, Creature self, int stolenAmount)
    {
        var mine = self.GetPower<HardToKillPower>();
        if (mine == null)
        {
            await PowerCmd.Apply<HardToKillPower>(ctx, self, stolenAmount, self, this);
            return;
        }

        int current = mine.Amount;
        if (stolenAmount >= current)
            return; // 已有层数更小或相等，保持更强的 cap

        // current > stolenAmount：降到较小层数
        await PowerCmd.ModifyAmount(ctx, mine, stolenAmount - current, self, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
