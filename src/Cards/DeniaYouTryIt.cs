#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>你也试试？ — Rare Attack, 0e. This turn, each subsequent card adds 2(4) burst cap, or Melts at full cap.</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaYouTryIt : DeniaCard
{
    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_you_try_it.png";

    public DeniaYouTryIt()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "你也试试？",
            Description: "打出此牌后，本回合内你每打出一张牌，给该敌人附加{IfUpgraded:show:4|2}点[color=#9A6A18]聚爆上限[/color]。若触发时上限已满40，改为熔解1{IfUpgraded:show:，且此熔解不消耗聚爆层数|}。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int amount = IsUpgraded ? 4 : 2;
        await PowerCmd.Apply<DeniaYouTryItPower>(ctx, play.Target, amount, Owner.Creature, this);
    }

    protected override void OnUpgrade() { }
}

public sealed class DeniaYouTryItPower : CustomPowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override bool IsVisibleInternal => false;

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "你也试试？",
        Description: "本回合内每打出一张牌，给目标敌人附加聚爆上限；满40改为熔解。",
        SmartDescription: "本回合内每打出一张牌，给目标敌人附加{Amount}聚爆上限；满40改为熔解。");

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // 每次打出独立保存目标和施加者；新状态在本牌效果中创建，不会触发自身。
        if (Owner.IsDead || Applier == null || cardPlay.Card.Owner.Creature != Applier) return;
        if (AemeathFusionBurstState.GetFusionBurstCap(Owner) >= 40)
        {
            if (Amount == 4)
            {
                using (DeniaMeltProtectPatch.BeginPreserve(cardPlay.Card))
                    await AemeathFusionBurstState.ResolveMelt(Owner, Applier, cardPlay.Card, 1);
            }
            else
                await AemeathFusionBurstState.ResolveMelt(Owner, Applier, cardPlay.Card, 1);
        }
        else
            await AemeathFusionBurstState.TryIncreaseFusionBurstCap(Owner, Amount, Applier, cardPlay.Card);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Player ? PowerCmd.Remove(this) : Task.CompletedTask;
}
