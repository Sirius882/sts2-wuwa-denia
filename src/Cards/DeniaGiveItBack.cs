#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using TuneStrain.Powers;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using TuneStrain;

namespace Denia;

/// <summary>还给你 — Uncommon Attack, 1e. 照抄原版 Misery 实现。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGiveItBack : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_give_it_back.png";

    public DeniaGiveItBack()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "还给你",
            Description: "若有多名敌人，将目标身上的负面效果（包括聚爆、额外聚爆上限、偏谐、干涉、集谐·干涉和集谐·偏移）给予其他敌人。若只有一名敌人，附加1集谐·偏移、8聚爆和5聚爆上限。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var combatState = Owner.Creature.CombatState;
        ArgumentNullException.ThrowIfNull(combatState);
        var otherEnemies = combatState.HittableEnemies.Where(enemy => enemy != play.Target && !enemy.IsDead).ToArray();
        if (otherEnemies.Length == 0)
        {
            await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, 5, Owner.Creature, this);
            await AemeathFusionBurstState.TryAddFusionBurst(play.Target, 8, Owner.Creature, this);
            return;
        }

        // 照抄 Misery：扩散前先克隆目标所有减益。
        var debuffAmounts = play.Target.Powers
            .Where(p => p is not TuneStrainInterferenceDurationPower
                && (p.TypeForCurrentAmount == PowerType.Debuff || p is AemeathFusionBurstCapPower))
            .Select(p => ((PowerModel)p.ClonePreservingMutability(), Amount: p.Amount))
            .ToDictionary();

        // Beta 将临时 Power 的防重复逻辑改为：把对应的内部 Power 数值合并进待复制结果。
        // 这样复制时不会再次额外施加临时 Power 的内部减益。
        foreach (var pair in debuffAmounts.ToArray())
        {
            if (pair.Key is not ITemporaryPower temporaryPower) continue;

            var internalPower = debuffAmounts.FirstOrDefault(
                entry => entry.Key.Id == temporaryPower.InternallyAppliedPower.Id);
            if (internalPower.Key != null)
                debuffAmounts[internalPower.Key] += pair.Value;
        }

        // 扩散给其他敌人（照抄 Misery 的 FindExistingInstanceForStacking 模式）
        foreach (var enemy in otherEnemies)
        {
            if (enemy.IsDead) continue;

            foreach (var debuff in debuffAmounts)
            {
                if (debuff.Value == 0) continue;

                if (debuff.Key is TuneStrainInterferencePower)
                {
                    await TuneStrainState.ApplyInterference(enemy, debuff.Value, debuff.Key.Applier ?? Owner.Creature, this);
                    continue;
                }

                var existing = PowerCmd.FindExistingInstanceForStacking(debuff.Key, enemy, debuff.Key.Applier);
                if (existing != null)
                {
                    await PowerCmd.ModifyAmount(ctx, existing, debuff.Value, debuff.Key.Applier, this);
                }
                else
                {
                    var cloned = (PowerModel)debuff.Key.ClonePreservingMutability();
                    await PowerCmd.Apply(ctx, cloned, enemy, debuff.Value, debuff.Key.Applier, this);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
