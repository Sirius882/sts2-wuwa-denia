using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

/// <summary>泡沫蜜饵 — Common Attack AOE.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaFoamBait : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_foam_bait.png";

    public DeniaFoamBait()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(Title: "泡沫蜜饵",
            Description: "对所有敌人造成{IfUpgraded:show:15|10}点伤害。\n虚质强化：伤害+5。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int damage = IsUpgraded ? 15 : 10;
        if (await TrySpendVirtualMatter(play)) damage += 5;

        await DamageCmd.Attack(damage)
            .FromCard(this).TargetingAllOpponents(Owner.Creature.CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
    }

    protected override void OnUpgrade() { }
}
