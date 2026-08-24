#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaSorrow : DeniaCard
{
    public override int CurrentVirtualMatterCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_sorrow.png";

    public DeniaSorrow()
        : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "哀",
            Description: "附加{IfUpgraded:show:2|1}点[color=#9A6A18]集谐·偏移[/color]、{IfUpgraded:show:2|1}点[color=#9A6A18]聚爆上限[/color]和{IfUpgraded:show:4|2}点[color=#9A6A18]聚爆[/color]，触发无条件[color=#9A6A18]谐度破坏[/color]。此次[color=#9A6A18]谐度破坏[/color]只造成五分之一的伤害。\n虚质强化：[color=#9A6A18]谐度破坏[/color]造成完整伤害。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int bias = IsUpgraded ? 2 : 1;
        int cap = IsUpgraded ? 2 : 1;
        int burst = IsUpgraded ? 4 : 2;
        bool fullRupture = await TrySpendVirtualMatter(play);

        await TuneStrainState.TryAddBias(play.Target, bias, Owner.Creature, this);
        // 先提高上限，再叠聚爆。
        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, cap, Owner.Creature, this);
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);

        if (fullRupture)
        {
            await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
        }
        else
        {
            await DeniaResonanceBreakDamageModifier.RunOnce(
                0.2m,
                () => AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this));
        }
    }

    protected override void OnUpgrade() { }
}
