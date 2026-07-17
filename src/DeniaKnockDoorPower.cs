using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>
/// 轻叩门扉的临时失去力量 debuff。
/// 逻辑同原版 TemporaryStrengthPower（负力量，回合结束清除并返还）。
/// 图标借用原版 Piercing Wail 的 power 图标。
/// </summary>
public sealed class DeniaKnockDoorStrengthLossPower : TemporaryStrengthPower, ICustomPower
{
    public override AbstractModel OriginModel => ModelDb.Card<DeniaKnockDoor>();
    protected override bool IsPositive => false;

    // 借用原版 Piercing Wail 的 power 图标（combat 小图标 + 大图标）
    public string? CustomPackedIconPath => "res://images/powers/piercing_wail_power.png";
    public string? CustomBigIconPath => "res://images/powers/piercing_wail_power.png";
    public string? CustomBigBetaIconPath => null;
}