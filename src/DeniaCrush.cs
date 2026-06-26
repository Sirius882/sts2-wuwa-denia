using System;using System.Collections.Generic;using System.Threading.Tasks;using AemeathWw.Scripts;using BaseLib.Abstracts;using BaseLib.Utils;using MegaCrit.Sts2.Core.Commands;using MegaCrit.Sts2.Core.Entities.Cards;using MegaCrit.Sts2.Core.GameActions.Multiplayer;using MegaCrit.Sts2.Core.Localization.DynamicVars;using MegaCrit.Sts2.Core.ValueProps;
namespace Denia;
public sealed class DeniaCrush : DeniaCard
{
    public override int CurrentVirtualMatterCost => 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(9m, ValueProp.Move) };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_crush.png";
    public DeniaCrush() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override List<(string, string)>? Localization => new CardLoc(Title: "轧碎", Description: "造成{Damage:diff()}点伤害，附加所造成伤害1/3的[gold]聚爆[/gold]。\n虚质3：附加比例改为1/2。");
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int dmg = DynamicVars.Damage.IntValue;
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target).WithHitFx("vfx/vfx_heavy_blunt").Execute(ctx);
        bool vmSpent = await TrySpendVirtualMatter(play);
        int burst = vmSpent ? (int)(dmg / 2.0) : (int)(dmg / 3.0);
        if (burst > 0) await AemeathFusionBurstState.TryAddFusionBurst(play.Target, burst, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6m);
}
