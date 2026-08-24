#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Denia;

/// <summary>虚质磁爆 — 事件专属技能。本回合锁定虚质；升级后也锁定黯核。</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaVirtualMatterMagneticBurst : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, CardKeyword.Retain };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_virtual_matter_magnetic_burst.png";

    public DeniaVirtualMatterMagneticBurst()
        : base(0, CardType.Skill, CardRarity.Event, TargetType.Self, showInCardLibrary: true) { }

    public override List<(string, string)>? Localization => new CardLoc(
        Title: "虚质磁爆",
        Description: "本回合内，虚质余额固定为20{IfUpgraded:show:，黯核余额固定为5|}。回合结束时，清空所有虚质和黯核。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await DeniaResourceState.SetVirtualMatter(Owner.Creature, DeniaResourceState.VirtualMatterMax, Owner.Creature, this);
        if (Owner.Creature.GetPower<DeniaVirtualMatterLockedPower>() == null)
            await PowerCmd.Apply<DeniaVirtualMatterLockedPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);

        if (IsUpgraded)
        {
            await DeniaResourceState.SetDarkCore(Owner.Creature, DeniaResourceState.DarkCoreMax, Owner.Creature, this);
            if (Owner.Creature.GetPower<DeniaDarkCoreLockedPower>() == null)
                await PowerCmd.Apply<DeniaDarkCoreLockedPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() { }
}

public abstract class DeniaResourceLockedPowerBase : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        if (Owner.GetPower<DeniaVirtualMatterLockedPower>() != this) return;

        await DeniaResourceState.SetVirtualMatter(Owner, 0, Owner, null!, ignoreLock: true);
        await DeniaResourceState.SetDarkCore(Owner, 0, Owner, null!, ignoreLock: true);
        await PowerCmd.Remove<DeniaVirtualMatterLockedPower>(Owner);
        await PowerCmd.Remove<DeniaDarkCoreLockedPower>(Owner);
    }
}

public sealed class DeniaVirtualMatterLockedPower : DeniaResourceLockedPowerBase
{
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "虚质磁爆",
        Description: "本回合虚质固定为20。回合结束时清空虚质和黯核。",
        SmartDescription: "本回合虚质固定为20。回合结束时清空虚质和黯核。");
}

public sealed class DeniaDarkCoreLockedPower : DeniaResourceLockedPowerBase
{
    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "虚质磁爆+",
        Description: "本回合黯核固定为5。回合结束时清空虚质和黯核。",
        SmartDescription: "本回合黯核固定为5。回合结束时清空虚质和黯核。");
}