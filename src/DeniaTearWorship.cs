#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>拜泪 — Rare Colorless Attack, 2e. 10 dmg x2 AoE, kills trigger execute on others.</summary>
[Pool(typeof(ColorlessCardPool))]
public sealed class DeniaTearWorship : DeniaCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DamageVar(10m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face.png";

    public DeniaTearWorship()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "拜泪",
        Description: "对所有敌人造成{Damage}点伤害2次。\n若此牌触发[gold]斩杀[/gold]，则杀死其他敌人。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        decimal dmg = DynamicVars.Damage.BaseValue;

        // 两次基础伤害（WithHitCount 确保活力每段生效），追踪斩杀
        var atk = await DamageCmd.Attack(dmg)
            .WithHitCount(2)
            .FromCard(this)
            .TargetingAllOpponents(Owner.Creature.CombatState)
            .Execute(ctx);
        bool anyKilled = atk.Results.SelectMany(x => x).Any(r => r.WasTargetKilled);

        // 若触发斩杀，杀死其余所有存活敌人
        if (anyKilled)
        {
            while (true)
            {
                var alive = CombatManager.Instance.DebugOnlyGetState()
                    ?.Enemies.Where(e => !e.IsDead).ToArray();
                if (alive == null || alive.Length == 0) break;
                foreach (var enemy in alive)
                {
                    enemy.RemoveAllPowersInternalExcept();
                    await CreatureCmd.Kill(enemy);
                }
            }
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
