using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace Denia;

/// <summary>大师之剑 — Rare Relic. 40-count attack tracker, grants 2 str at combat start when counter > 0.</summary>
[BaseLib.Utils.Pool(typeof(SharedRelicPool))]
public sealed class DeniaMasterSword : CustomRelicModel
{
    static DeniaMasterSword()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DeniaMasterSword));
    }

    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override string IconBaseName => "denia_master_sword";

    /// <summary>当前攻击牌计数，会被存档。</summary>
    [SavedProperty]
    internal int Counter { get; set; } = 40;

    internal decimal GrantedStrength;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress && !IsCanonical;

    public override int DisplayAmount
    {
        get
        {
            GD.Print($"[MasterSword] DisplayAmount: inProgress={CombatManager.Instance.IsInProgress}, canonical={IsCanonical}, counter={Counter}");
            if (!CombatManager.Instance.IsInProgress || IsCanonical) return -1;
            return Counter;
        }
    }

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            Title: "大师之剑",
            Description: $"初始拥有40点计数。每打出一张攻击牌，消耗1计数。战斗开始时若计数>0，获得2力量；计数归零时失去力量。Boss战中不受计数限制且不消耗计数。Boss战胜利后计数恢复40。",
            Flavor: "大师之剑是一把神圣的剑，邪恶之人永远无法触碰……只有配得上「时之勇者」称号的人，才能将它从时之神殿的台座上拔起。");

    public override async Task AfterObtained()
    {
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatWon += OnCombatWon;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        // 保底：起始遗物不会触发 AfterObtained，用 AfterRoomEntered 降级兜底
        CombatManager.Instance.CombatSetUp -= OnCombatSetUp;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatWon -= OnCombatWon;
        CombatManager.Instance.CombatWon += OnCombatWon;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    internal void OnCombatSetUp(CombatState state)
    {
        if (Owner == null) return;
        bool isBoss = Owner.RunState.CurrentRoom.RoomType == RoomType.Boss;

        if ((!isBoss && Counter > 0) || isBoss)
        {
            _ = MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                Owner.Creature, 2m, Owner.Creature, null!);
            GrantedStrength = 2m;
        }
    }

    internal void OnCombatWon(CombatRoom room)
    {
        if (room.RoomType == RoomType.Boss)
            Counter = 40;
    }

    internal void OnCombatEnded(CombatRoom room)
    {
        GrantedStrength = 0;
    }

    /// <summary>供外部补丁调用，刷新遗物 UI 计数。</summary>
    public void RefreshDisplay() => InvokeDisplayAmountChanged();
}
