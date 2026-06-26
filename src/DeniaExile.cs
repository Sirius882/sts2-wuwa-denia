using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
public sealed class DeniaExile : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_exile.png";
    public DeniaExile() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "放逐", Description: "造成{Damage:diff()}点伤害{IfUpgraded:show:3|2}次。\n抽{IfUpgraded:show:2|1}张牌。\n黯核强化：恢复1点能量。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); int hits = IsUpgraded ? 3 : 2; await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits).FromCard(this).Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(ctx); await CardPileCmd.Draw(ctx, IsUpgraded ? 2 : 1, Owner); if (await TrySpendDarkCore(play)) await PlayerCmd.GainEnergy(1, Owner); }
    protected override void OnUpgrade() { }
}
