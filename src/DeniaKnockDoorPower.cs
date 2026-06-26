using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

public sealed class DeniaKnockDoorStrengthLossPower : MegaCrit.Sts2.Core.Models.Powers.TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<DeniaKnockDoor>();
    protected override bool IsPositive => false;
}
