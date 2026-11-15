using System.Threading.Tasks;
using Godot;

// Only game services are stand-ins; the production execution rule and overlay are source-linked.
namespace AemeathWw.Scripts
{
    public interface IAemeathResonanceBreakRule
    {
        int Priority { get; }
        bool ShouldApply(AemeathResonanceBreakContext context);
        Task Apply(AemeathResonanceBreakContext context);
    }
    public sealed class AemeathResonanceBreakContext
    {
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Target = new();
    }
}
namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public sealed class Creature
    {
        public bool IsDead;
        public T? GetPower<T>() where T : class => null;
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public partial class NCombatRoom : Node2D
    {
        public static NCombatRoom? Instance;
        public Node2D? GetCreatureNode(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => null;
    }
}
namespace MegaCrit.Sts2.Core.Helpers { public static class NonInteractiveMode { public static bool IsActive; } }
namespace TuneStrain
{
    public static class TuneStrainState
    {
        public static int GetBias(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => 3;
    }
}
