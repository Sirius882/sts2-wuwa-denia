#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

[HarmonyPatch(typeof(SandpitPower), nameof(SandpitPower.AfterRemoved))]
internal static class DeniaSandpitRemovalPatch
{
    private static readonly HashSet<SandpitPower> Dispelled = new();

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

    // 原版在倒计时耗尽时移除沙坑，AfterRemoved 会吞噬被困角色。主动夺取时只解除沙坑。
    private static bool Prefix(SandpitPower __instance, float ____initialTargetPosition, ref Task __result)
    {
        if (!Dispelled.Contains(__instance)) return true;

        var target = __instance.Target;
        var room = NCombatRoom.Instance;
        var playerNode = target == null ? null : room?.GetCreatureNode(target);
        if (target != null && playerNode != null)
        {
            var position = playerNode.GlobalPosition;
            float offset = ____initialTargetPosition - position.X;
            playerNode.GlobalPosition = position with { X = ____initialTargetPosition };
            foreach (var pet in target.Pets)
            {
                var node = room?.GetCreatureNode(pet);
                if (node != null)
                    node.GlobalPosition += new Godot.Vector2(offset, 0);
            }
            if (LocalContext.IsMe(target))
                NRunMusicController.Instance?.UpdateMusicParameter(TheInsatiable.TheInsatiableTrackName, 0);
        }
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
