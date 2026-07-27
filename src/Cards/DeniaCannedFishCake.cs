using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using TuneStrain;

namespace Denia;

/// <summary>
/// 鱼罐头松糕 — Uncommon Skill, 1e, Exhaust.
/// 本回合集谐响应层数按 2 倍计算；与共鸣模态·集谐(1.5)同时存在时相乘为 3 倍。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaCannedFishCake : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_canned_fish_cake.png";

    public DeniaCannedFishCake()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "鱼罐头松糕",
        Description: "本回合内，[gold]集谐响应[/gold]层数按2倍计算。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        _ = play;
        await PowerCmd.Apply<DeniaCannedFishCakePower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class DeniaCannedFishCakePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    static DeniaCannedFishCakePower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaCannedFishCakePower));
        TuneStrainState.RegisterResponseDegreeMultiplier(creature =>
        {
            var power = creature.GetPower<DeniaCannedFishCakePower>();
            return power == null || power.Amount <= 0 ? 1.0 : System.Math.Pow(2.0, (double)power.Amount);
        });
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        await PowerCmd.Remove<DeniaCannedFishCakePower>(Owner);
    }
}
