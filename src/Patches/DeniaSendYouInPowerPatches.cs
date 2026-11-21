#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

[HarmonyPatch(typeof(SandpitPower))]
internal static class DeniaSandpitRemovalPatch
{
    private sealed record InitialPosition(float X);

    private static readonly HashSet<SandpitPower> Dispelled = new();
    private static readonly ConditionalWeakTable<SandpitPower, InitialPosition> Positions = new();

    internal static async Task RemoveWithoutFinisher(SandpitPower power)
    {
        Dispelled.Add(power);
        try
        {
            await PowerCmd.Remove(power);
        }
        finally
        {
            Dispelled.Remove(power);
        }
    }

    // 以角色节点的局部坐标记录站位，避免分辨率、镜头和联机两端的变换影响还原。
    [HarmonyPostfix, HarmonyPatch(nameof(SandpitPower.AfterApplied))]
    private static void RememberPosition(SandpitPower __instance)
    {
        if (__instance.Target is { } target && NCombatRoom.Instance?.GetCreatureNode(target) is { } node)
            Positions.AddOrUpdate(__instance, new InitialPosition(node.Position.X));
    }

    // 原版在倒计时耗尽时移除沙坑，AfterRemoved 会吞噬被困角色。主动夺取时只解除沙坑。
    [HarmonyPrefix, HarmonyPatch(nameof(SandpitPower.AfterRemoved))]
    private static bool BeforeRemoved(SandpitPower __instance, ref Task __result)
    {
        if (!Dispelled.Contains(__instance)) return true;

        var target = __instance.Target;
        var room = NCombatRoom.Instance;
        var playerNode = target == null ? null : room?.GetCreatureNode(target);
        if (target != null && playerNode != null && Positions.TryGetValue(__instance, out var start))
        {
            float previousX = playerNode.GlobalPosition.X;
            playerNode.Position = playerNode.Position with { X = start.X };
            float offset = playerNode.GlobalPosition.X - previousX;
            foreach (var pet in target.Pets)
            {
                var node = room?.GetCreatureNode(pet);
                if (node != null)
                    node.GlobalPosition += new Godot.Vector2(offset, 0);
            }
            if (LocalContext.IsMe(target))
                NRunMusicController.Instance?.UpdateMusicParameter(TheInsatiable.TheInsatiableTrackName, 0);
        }
        Positions.Remove(__instance);
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class DeniaGardenerHitAnimationPatch
{
    private static readonly MethodInfo HasGainedBlock = AccessTools.PropertyGetter(
        typeof(SkittishPower), nameof(SkittishPower.HasGainedBlockThisTurn));

    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(PhantasmalGardener))
            .Where(method => method.Name.StartsWith("<GenerateAnimator>", System.StringComparison.Ordinal)
                && PatchProcessor.GetOriginalInstructions(method).Any(instruction => instruction.Calls(HasGainedBlock)));

    // 园丁被夺走胆小后，受击动画选择普通（未缩壳）分支。
    private static bool HasGainedBlockOrFalse(SkittishPower? power) => power?.HasGainedBlockThisTurn ?? false;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(HasGainedBlock,
            AccessTools.Method(typeof(DeniaGardenerHitAnimationPatch), nameof(HasGainedBlockOrFalse)));
}

[HarmonyPatch(typeof(ReattachPower), "GetOtherSegments")]
internal static class DeniaReattachSegmentsPatch
{
    // 接续仍在的节段应能依附于任何存活的同类。否则原版会提前移除失去接续的活节段的节点。
    private static void Postfix(ReattachPower __instance, ref IEnumerable<Creature> __result)
    {
        var owner = __instance.Owner;
        if (owner.Monster is not DecimillipedeSegment || owner.CombatState == null) return;

        __result = __result.Concat(owner.CombatState.GetTeammatesOf(owner).Where(creature =>
            creature != owner && creature.IsAlive && creature.Monster is DecimillipedeSegment
                && !creature.HasPower<ReattachPower>()));
    }
}

[HarmonyPatch(typeof(CrabRagePower), nameof(CrabRagePower.AfterDeath))]
internal static class DeniaCrabRageDeathPatch
{
    // 瓶中精灵等免死也会进入 AfterDeath。玩家取得的蟹之怒只响应实际阵亡。
    private static bool Prefix(CrabRagePower __instance, bool wasRemovalPrevented, ref Task __result)
    {
        if (!wasRemovalPrevented || !__instance.Owner.IsPlayer) return true;

        __result = Task.CompletedTask;
        return false;
    }
}
