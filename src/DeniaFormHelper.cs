using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

public enum DeniaBlackBuffKind
{
    None,
    StrengthOnly,
    TrajectoryOnly,
    Both
}
public static class DeniaFormHelper
{
    /// <summary>本回合是否切换过形态（用于泡泡机等判定）。</summary>
    public static bool _formSwitchedThisTurn;

    private static readonly PlayerChoiceContext _throwing = new ThrowingPlayerChoiceContext();
    private static readonly Dictionary<Creature, DeniaBlackBuffKind> _buffKind = new();
    private static readonly Dictionary<Creature, int> _recordedStrength = new();
    private static readonly Dictionary<Creature, int> _recordedTrajectory = new();
    private static readonly Dictionary<Creature, int> _forgiveMeStrength = new();
    private static readonly Dictionary<Creature, int> _forgiveMeTrajectory = new();

    public static DeniaForm GetForm(Creature creature)
    {
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null || power.Amount <= 0) return DeniaForm.Pink;
        return DeniaForm.Black;
    }

    public static bool IsPink(Creature creature) => GetForm(creature) == DeniaForm.Pink;
    public static bool IsBlack(Creature creature) => GetForm(creature) == DeniaForm.Black;

    public static DeniaBlackBuffKind GetBuffKind(Creature creature)
        => _buffKind.TryGetValue(creature, out var kind) ? kind : DeniaBlackBuffKind.None;

    public static void SetBuffKind(Creature creature, DeniaBlackBuffKind kind)
    {
        if (_buffKind.TryGetValue(creature, out var existing) && existing != DeniaBlackBuffKind.None && existing != kind)
            _buffKind[creature] = DeniaBlackBuffKind.Both;
        else
            _buffKind[creature] = kind;
    }

    /// <summary>记录直视我通过虚质强化给予的力量值。</summary>
    public static void RecordLookAtMeStrength(Creature creature, int amount)
    {
        _recordedStrength[creature] = amount;
    }

    /// <summary>记录怜悯我通过虚质强化给予的轨迹值。</summary>
    public static void RecordPityMeTrajectory(Creature creature, int amount)
    {
        _recordedTrajectory[creature] = amount;
    }

    /// <summary>记录宽恕我通过虚质强化给予的力量值。</summary>
    public static void RecordForgiveMeStrength(Creature creature, int amount)
    {
        _forgiveMeStrength[creature] = amount;
    }

    /// <summary>记录宽恕我通过虚质强化给予的轨迹值。</summary>
    public static void RecordForgiveMeTrajectory(Creature creature, int amount)
    {
        _forgiveMeTrajectory[creature] = amount;
    }

    /// <summary>通用记录力量值（虚质粒子/久疏问候黯核强化用）。</summary>
    public static void RecordStrength(Creature creature, int amount)
    {
        _recordedStrength[creature] = amount;
    }

    /// <summary>通用记录聚爆轨迹值（虚质粒子/久疏问候黯核强化用）。</summary>
    public static void RecordTrajectory(Creature creature, int amount)
    {
        _recordedTrajectory[creature] = amount;
    }

    public static async Task SwitchToBlack(Creature creature, Creature applier, CardModel source)
    {
        _formSwitchedThisTurn = true;
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null)
            await PowerCmd.Apply<DeniaFormPower>(_throwing, creature, 1m, applier, source);
        else if (power.Amount <= 0)
            await PowerCmd.ModifyAmount(_throwing, power, 1m, applier, source);
        DeniaFormPatch.RefreshForCreature(creature);
        await DeniaResourceState.GainVirtualMatter(creature, 10, applier, source);
        await ApplyFormSwitchEffects(creature, applier, source);
    }

    public static async Task SwitchToPink(Creature creature, Creature applier, CardModel source, bool clearVM = true)
    {
        _formSwitchedThisTurn = true;
        var power = creature.GetPower<DeniaFormPower>();
        if (power == null || power.Amount <= 0) return;

        // 记录切换前的虚质（用于“黑变粉≥7虚质时抽牌回能”基础机制）
        int vmBeforeSwitch = DeniaResourceState.GetVirtualMatter(creature);

        await PowerCmd.ModifyAmount(_throwing, power, -1m, applier, source);

        if (_buffKind.TryGetValue(creature, out var kind) && kind != DeniaBlackBuffKind.None)
        {
            if (kind == DeniaBlackBuffKind.StrengthOnly || kind == DeniaBlackBuffKind.Both)
            {
                int strToRemove = _recordedStrength.TryGetValue(creature, out var s) ? s
                    : _forgiveMeStrength.TryGetValue(creature, out var fs) ? fs : 0;
                var str = creature.GetPower<StrengthPower>();
                if (str != null && str.Amount >= strToRemove && strToRemove > 0)
                    await PowerCmd.ModifyAmount(_throwing, str, -strToRemove, applier, source);
            }
            if (kind == DeniaBlackBuffKind.TrajectoryOnly || kind == DeniaBlackBuffKind.Both)
            {
                int trajToRemove = _recordedTrajectory.TryGetValue(creature, out var t) ? t
                    : _forgiveMeTrajectory.TryGetValue(creature, out var ft) ? ft : 0;
                var traj = creature.GetPower<AemeathFusionBurstTrajectoryPower>();
                if (traj != null && traj.Amount >= trajToRemove && trajToRemove > 0)
                    await PowerCmd.ModifyAmount(_throwing, traj, -trajToRemove, applier, source);
            }
        }

        if (clearVM)
            await DeniaResourceState.ClearVirtualMatter(creature, applier, source);
        _buffKind[creature] = DeniaBlackBuffKind.None;
        _recordedStrength.Remove(creature);
        _recordedTrajectory.Remove(creature);
        _forgiveMeStrength.Remove(creature);
        _forgiveMeTrajectory.Remove(creature);
        DeniaFormPatch.RefreshForCreature(creature);

        // 基础机制：黑变粉时，若切换前虚质≥7，抽1张牌并恢复1能量
        if (vmBeforeSwitch >= 7)
        {
            await CardPileCmd.Draw(_throwing, 1, creature.Player);
            await PlayerCmd.GainEnergy(1m, creature.Player);
        }

        await ApplyFormSwitchEffects(creature, applier, source);
    }

    /// <summary>形态切换后分发相关能力效果（夏耶/卡纽/阿马罗）。</summary>
    private static async Task ApplyFormSwitchEffects(Creature creature, Creature applier, CardModel source)
    {
        var xiaYe = creature.GetPower<DeniaXiaYePower>();
        if (xiaYe != null && xiaYe.Amount > 0)
            await DeniaResourceState.GainDarkCore(creature, (int)xiaYe.Amount, applier, source);

        var kaNiu = creature.GetPower<DeniaKaNiuPower>();
        if (kaNiu != null && kaNiu.Amount > 0)
            await PlayerCmd.GainEnergy((int)kaNiu.Amount, creature.Player);

        var amaro = creature.GetPower<DeniaAmaroPower>();
        if (amaro != null && amaro.Amount > 0)
            await CardPileCmd.Draw(_throwing, (int)amaro.Amount, creature.Player);
    }

    public static Creature? PickRandomEnemy(Player player)
    {
        var combatState = player.Creature.CombatState;
        var hittable = combatState.HittableEnemies;
        if (hittable.Count == 0) return null;
        return player.RunState.Rng.CombatTargets.NextItem(hittable);
    }
}
