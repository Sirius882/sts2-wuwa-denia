using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

/// <summary>
/// 止痛药：仅对持有者生效（开场 5 力量/敏捷/蔽星；每回合开始自身 1 易伤）。
/// 效果写在遗物生命周期里，避免全局 AfterSideTurnStart 误扫到其他玩家。
/// </summary>
[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaPainkiller : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    protected override string IconBaseName => "denia_painkiller";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "止痛药",
            Description: "每场战斗开始时，获得5点[gold]力量[/gold]、5点[gold]敏捷[/gold]和5层[gold]蔽星[/gold]。每个回合开始时，获得1层[gold]易伤[/gold]。",
            Flavor: "被残星会会长换成了生理盐水，并没有止痛的效果。");

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        // 只在持有者自己参与的玩家回合生效
        if (side != CombatSide.Player) return;
        if (Owner?.Creature == null || Owner.Creature.IsDead) return;
        if (!participants.Contains(Owner.Creature)) return;

        var self = Owner.Creature;
        var ctx = new ThrowingPlayerChoiceContext();

        if (self.GetPower<DeniaPainkillerOpeningBuffUsedPower>() == null)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(ctx, self, 5m, self, null!);
            await PowerCmd.Apply<DexterityPower>(ctx, self, 5m, self, null!);
            await PowerCmd.Apply<DeniaShroudedStarPower>(ctx, self, 5m, self, null!);
            await PowerCmd.Apply<DeniaPainkillerOpeningBuffUsedPower>(ctx, self, 1m, self, null!);
        }

        Flash();
        await PowerCmd.Apply<VulnerablePower>(ctx, self, 1m, self, null!);
    }
}
