using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaInspectionReportRa2499G : DeniaCard
{
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_inspection_report_ra2499_g.png";
    public DeniaInspectionReportRa2499G() : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "检验报告RA2499-G",
        Description: "本场战斗中，你的[color=#9A6A18]聚爆[/color]伤害不再是AOE伤害，而是把本应造成的AOE伤害总量全部集中在被引爆的单个目标上。");
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        PowerCmd.Apply<DeniaInspectionReportPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
