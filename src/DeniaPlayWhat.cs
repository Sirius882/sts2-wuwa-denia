#nullable enable
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
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaPlayWhat : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_play_what.png";

    public DeniaPlayWhat()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "最后的谎言",
        Description: "附加1[gold]集谐·偏移[/gold]，造成{Damage:diff()}点伤害。\n虚质强化：伤害+8。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await TuneStrainState.TryAddBias(play.Target, 1, Owner.Creature, this);
        decimal damage = DynamicVars.Damage.BaseValue;
        if (await TrySpendVirtualMatter(play))
            damage += 8m;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
