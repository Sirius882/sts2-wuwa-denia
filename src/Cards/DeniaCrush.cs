using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>
/// 轧碎 — Rare Power。本场战斗中，你附加的聚爆引爆伤害集中到被引爆的单个目标
/// （逐目标按易伤/增伤预结算后求和；敌方仅1个目标时按2份计算）。
/// 实际分配由 DeniaCrushDistributionPatch 接管。
/// </summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaCrush : DeniaCard
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_crush.png";

    public DeniaCrush() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "轧碎",
        Description: "本场战斗中，你的[color=#9A6A18]聚爆引爆[/color]伤害不再AOE结算，而是把本应造成的伤害总量全部集中在被引爆的单个目标上。触发时，如果敌方阵营只有1个目标，就按有2个目标计算。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DeniaCrushPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
        EnergyCost.UpgradeBy(-1);
    }
}
