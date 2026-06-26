using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

/// <summary>楔丸 — Common Relic. 回合结束时，若敌人受到的伤害等于其意图伤害，击晕之。</summary>
[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaKusabimaru : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override string IconBaseName => "denia_kusabimaru";

    /// <summary>当前回合每个敌人累计受到的非格挡伤害。</summary>
    internal static readonly Dictionary<MegaCrit.Sts2.Core.Entities.Creatures.Creature, int> TurnDamage = new();

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "楔丸",
            Description: "在回合结束时，若本回合内有敌人受到的伤害正好等于其攻击意图的总伤害，令其眩晕。",
            Flavor: "弹反一切之刀");
}
