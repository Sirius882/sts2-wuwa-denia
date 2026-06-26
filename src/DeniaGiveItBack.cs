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

namespace Denia;

/// <summary>还给你 — Rare Attack, 0e. 照抄原版 Misery 实现。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaGiveItBack : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(7m, ValueProp.Move) };

    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_give_it_back.png";

    public DeniaGiveItBack()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "还给你",
        Description: "造成{Damage}点伤害。将此名敌人身上的负面效果给予其他敌人。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        // 照抄 Misery: 伤害前先克隆目标所有减益
        var originalDebuffs = play.Target.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
            .Select(p => (PowerModel)p.ClonePreservingMutability())
            .ToList();

        // 打伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 扩散给其他敌人（照抄 Misery 的 FindExistingInstanceForStacking 模式）
        foreach (var enemy in Owner.Creature.CombatState.HittableEnemies)
        {
            if (enemy == play.Target || enemy.IsDead) continue;

            foreach (var debuff in originalDebuffs)
            {
                var existing = PowerCmd.FindExistingInstanceForStacking(debuff, enemy, debuff.Applier);
                if (existing != null)
                {
                    DoHackyThings(existing);
                    await PowerCmd.ModifyAmount(ctx, existing, debuff.Amount, debuff.Applier, this);
                }
                else
                {
                    var cloned = (PowerModel)debuff.ClonePreservingMutability();
                    DoHackyThings(cloned);
                    await PowerCmd.Apply(ctx, cloned, enemy, debuff.Amount, debuff.Applier, this);
                }
            }
        }
    }

    private static void DoHackyThings(PowerModel power)
    {
        if (power is ITemporaryPower temp)
            temp.IgnoreNextInstance();
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        AddKeyword(CardKeyword.Retain);
    }
}
