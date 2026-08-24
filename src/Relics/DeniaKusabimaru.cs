using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

/// <summary>楔丸 — Uncommon Relic. 回合结束时，若受伤与意图伤害差值绝对值≤2，击晕之。</summary>
[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaKusabimaru : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override string IconBaseName => "denia_kusabimaru";

    /// <summary>当前回合每个敌人累计受到的非格挡伤害。</summary>
    internal static readonly Dictionary<MegaCrit.Sts2.Core.Entities.Creatures.Creature, int> TurnDamage = new();

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "楔丸",
            Description: "在回合结束时，若本回合内，有敌人受到的伤害与其攻击意图的总伤害差值不大于2，令其眩晕。",
            Flavor: "弹反一切之刀");
}
