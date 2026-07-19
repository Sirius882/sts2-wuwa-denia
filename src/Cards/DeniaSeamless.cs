using System;using System.Collections.Generic;using System.Threading.Tasks;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
public sealed class DeniaSeamless : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8m, ValueProp.Move), new BlockVar(8m, ValueProp.Move) };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_seamless.png";
    public override bool GainsBlock => true;
    public DeniaSeamless() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "天衣无缝", Description: "造成{Damage:diff()}点伤害。\n获得{Block:diff()}点[gold]格挡[/gold]。\n虚质强化：伤害和格挡+3。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); var dmg = DynamicVars.Damage.BaseValue; var blk = DynamicVars.Block.BaseValue; if (await TrySpendVirtualMatter(play)) { dmg += 3m; blk += 3m; } await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(ctx); await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(blk, ValueProp.Move), play); }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(2m); DynamicVars.Block.UpgradeValueBy(2m); }
}
