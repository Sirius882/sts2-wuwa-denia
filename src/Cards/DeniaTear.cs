using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

/// <summary>撕裂 — Uncommon Attack: 15 dmg (upg 21); VM +8.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaTear : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(15m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_tear.png";

    public DeniaTear()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "撕裂",
            Description: "造成{Damage:diff()}点伤害。\n虚质强化：伤害+8。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target, "play.Target");

        decimal dmg = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            dmg += 8m;

        await DamageCmd.Attack(dmg)
            .WithHitCount(1)
            .FromCard(this).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
