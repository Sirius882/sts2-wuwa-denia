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

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaLookAtThis : DeniaCard
{
    public override int CurrentVirtualMatterCost => 5;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(1m, ValueProp.Move) };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_look_at_this.png";

    public DeniaLookAtThis()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "你看见了什么？",
            Description: "对所有敌人附加1[color=#9A6A18]集谐·偏移[/color]。造成{Damage:diff()}点伤害。从牌组中选择{IfUpgraded:show:4|2}张牌，附加[color=#9A6A18]集谐响应[/color]。\n虚质强化：多附加1[color=#9A6A18]集谐·偏移[/color]。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var combatState = Owner.Creature.CombatState;
        ArgumentNullException.ThrowIfNull(combatState);

        int bias = await TrySpendVirtualMatter(play) ? 2 : 1;
        var enemies = combatState.Enemies.Where(enemy => !enemy.IsDead).ToArray();
        foreach (var enemy in enemies)
            await TuneStrainState.TryAddBias(enemy, bias, Owner.Creature, this);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        int count = IsUpgraded ? 4 : 2;
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

    protected override void OnUpgrade() { }
}