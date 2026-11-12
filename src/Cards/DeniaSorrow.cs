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
    public override int CurrentDarkCoreCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_sorrow.png";

    public DeniaSorrow()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "哀",
            Description: "附加1点集谐·偏移、{IfUpgraded:show:5|3}点聚爆上限和{IfUpgraded:show:5|3}聚爆。\n黯核强化：无条件谐度破坏。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        int bias = 1;
        int cap = IsUpgraded ? 5 : 3;
        int burst = IsUpgraded ? 5 : 3;

        await TuneStrainState.TryAddBias(play.Target, bias, Owner.Creature, this);
        // 先提高上限，再叠聚爆。
        await AemeathFusionBurstState.TryIncreaseFusionBurstCap(play.Target, cap, Owner.Creature, this);
        await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);

        if (await TrySpendDarkCore(play))
            await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(play.Target, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}
