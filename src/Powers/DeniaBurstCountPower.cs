using System.Linq;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

// Amount = 本场引爆次数 + 1，保留零次时的监听器；战斗结束随 power 清除。
public sealed class DeniaBurstCountPower : DeniaHiddenCounterPower, IOnAutoBurstPower
{
    public async Task OnAutoBurst(Creature target, Creature? applier, CardModel? source)
    {
        if ((applier is { IsPlayer: true } ? applier : source?.Owner?.Creature) != Owner) return;
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, 1, Owner, source);
        if (Owner.Player?.PlayerCombatState is not { } state) return;
        foreach (var card in state.AllCards.OfType<DeniaCrush>())
            card.EnergyCost.AddThisCombat(-1);
    }
}
