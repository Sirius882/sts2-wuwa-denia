using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
public sealed class DeniaExile : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_exile.png";
    public DeniaExile() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "放逐", Description: "造成{Damage:diff()}点伤害。抽{IfUpgraded:show:2|1}张牌。\n黯核强化：抽2张牌。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(ctx); await CardPileCmd.Draw(ctx, IsUpgraded ? 2 : 1, Owner); if (await TrySpendDarkCore(play)) await CardPileCmd.Draw(ctx, 2, Owner); }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(3m); }
}
