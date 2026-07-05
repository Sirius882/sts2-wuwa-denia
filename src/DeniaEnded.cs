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
public sealed class DeniaEnded : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_ended.png";

    public DeniaEnded()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "做个了断",
        Description: "造成{Damage:diff()}点伤害。\n虚质强化：再造成一次等量伤害。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        bool repeat = await TrySpendVirtualMatter(play);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
        if (repeat)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this)
                .Targeting(play.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(ctx);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}