using HarmonyLib;
using Godot;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Denia;

/// <summary>
/// Harmony 补丁：在 NCombatUi 激活时，为 Denia 角色挂载虚质计数器。
/// 计数器作为 NEnergyCounter 的子节点，定位在能量计数器附近。
/// </summary>
[HarmonyPatch(typeof(NCombatUi), "Activate")]
public static class DeniaVirtualMatterCounterPatch
{
    private static readonly AccessTools.FieldRef<NCombatUi, NEnergyCounter> EnergyCounterRef =
        AccessTools.FieldRefAccess<NCombatUi, NEnergyCounter>("_energyCounter");

    private static readonly Vector2 VirtualMatterCounterPosition = new(-44f, 26f);

    /// <summary>
    /// 安全获取本地玩家：优先用 NetId + GetPlayer，兼容单人/多人；
    /// 若失败则回退到首个玩家。
    /// </summary>
    private static Player? GetLocalPlayer(CombatState state)
    {
        ulong? netId = LocalContext.NetId;
        if (netId.HasValue)
        {
            Player? local = state.GetPlayer(netId.Value);
            if (local != null) return local;
        }
        return state.Players.FirstOrDefault();
    }

    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        Player? me = GetLocalPlayer(state);
        if (me == null) return;
        if (me.Character is not Denia) return;

        NEnergyCounter energyCounter = EnergyCounterRef(__instance);
        if (energyCounter == null) return;

        Node existing = energyCounter.GetNodeOrNull(nameof(DeniaVirtualMatterCounter));
        existing?.QueueFree();

        var counter = DeniaVirtualMatterCounter.Create(me);
        energyCounter.AddChild(counter);
        counter.Position = VirtualMatterCounterPosition;
    }
}
