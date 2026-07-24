#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain;

namespace Denia;

/// <summary>最后的谎言 — Common Attack: bias 1, dmg 1/3, select 2/3 deck cards for temp response.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaLastLie : DeniaCard
{
    public override int CurrentVirtualMatterCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(1m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_last_lie.png";

    public DeniaLastLie()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "最后的谎言",
        Description: "附加1[gold]集谐·偏移[/gold]，造成{Damage:diff()}点伤害，在牌组中选择{IfUpgraded:show:3|2}张牌附加[gold]集谐响应[/gold]。\n虚质强化：伤害+8。");

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

        int count = IsUpgraded ? 3 : 2;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "DENIA_TO_TUNE_STRAIN_RESPONSE"), count);
        var selected = await CardSelectCmd.FromDeckGeneric(
            Owner,
            prefs,
            card => card != DeckVersion
                && !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
                && !TuneStrainState.HasTemporaryResponse(card));

        foreach (var card in selected.ToList())
            TuneStrainState.AddTemporaryResponse(Owner, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
