#nullable enable
using System;
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

/// <summary>还给你 — Rare Attack, 0e. 照抄原版 Misery 实现。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGiveItBack : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(7m, ValueProp.Move) };

    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_give_it_back.png";

    public DeniaGiveItBack()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "还给你",
            Description: "造成{Damage}点伤害。将此名敌人身上的负面效果给予其他敌人。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        // 照抄 Misery: 伤害前先克隆目标所有减益
        var debuffAmounts = play.Target.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff || p is AemeathFusionBurstCapPower)
            .Select(p => ((PowerModel)p.ClonePreservingMutability(), Amount: p.Amount))
            .ToDictionary();

        // Beta 将临时 Power 的防重复逻辑改为：把对应的内部 Power 数值合并进待复制结果。
        // 这样复制时不会再次额外施加临时 Power 的内部减益。
        foreach (var pair in debuffAmounts)
        {
            if (pair.Key is not ITemporaryPower temporaryPower) continue;

            var internalPower = debuffAmounts.FirstOrDefault(
                entry => entry.Key.Id == temporaryPower.InternallyAppliedPower.Id);
            if (internalPower.Key != null)
                debuffAmounts[internalPower.Key] += pair.Value;
        }

        // 打伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        var combatState = Owner.Creature.CombatState;
        ArgumentNullException.ThrowIfNull(combatState);

        // 扩散给其他敌人（照抄 Misery 的 FindExistingInstanceForStacking 模式）
        foreach (var enemy in combatState.HittableEnemies)
        {
            if (enemy == play.Target || enemy.IsDead) continue;

            foreach (var debuff in debuffAmounts)
            {
                if (debuff.Value == 0) continue;

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
        DynamicVars.Damage.UpgradeValueBy(2m);
        AddKeyword(CardKeyword.Retain);
    }
}
