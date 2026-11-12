#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using AemeathWw.Scripts.Api;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using TuneStrain;

namespace Denia;

[Pool(typeof(DeniaCardPool))]
public sealed class DeniaProofOfPersonality : DeniaCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };

    public override string PortraitPath =>
        "res://images/packed/card_portraits/denia/card_face_proof_of_personality.png";

    public DeniaProofOfPersonality()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override List<(string, string)>? Localization => new CardLoc(Title: "人格的证明",
            Description: "每次附加[color=#9A6A18]集谐·偏移[/color]时，如果附加后集谐·偏移少于2点，则追加1点；如果附加后集谐·偏移不少于2点，自动触发无条件[color=#9A6A18]谐度破坏[/color]。不可叠加。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<DeniaProofOfPersonalityPower>() != null) return;

        await PowerCmd.Apply<DeniaProofOfPersonalityPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 每次附加集谐·偏移后：目标偏移少于2点则追加1点（追加本身在守卫内、不再触发本效果）；
/// 不少于2点自动触发无条件谐度破坏。通过 TuneStrainState.RegisterBiasAppliedHandler
/// 注册回调实现，回调在 TryAddBias 的 await 链内执行，无需 Harmony patch。
/// </summary>
public sealed class DeniaProofOfPersonalityPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://images/ui/powers/denia_proof_of_personality_power.webp";
    public override string? CustomBigIconPath => "res://images/ui/powers/denia_proof_of_personality_power.webp";

    public override List<(string, string)>? Localization => new PowerLoc(
        Title: "人格的证明",
        Description: "每次附加集谐·偏移时，如果附加后集谐·偏移少于2点，则追加1点；如果附加后集谐·偏移不少于2点，自动触发无条件谐度破坏。",
        SmartDescription: "每次附加集谐·偏移时，如果附加后集谐·偏移少于2点，则追加1点；如果附加后集谐·偏移不少于2点，自动触发无条件谐度破坏。");

    static DeniaProofOfPersonalityPower()
    {
        TuneStrainState.RegisterBiasAppliedHandler(async (target, applier, source) =>
        {
            if (applier?.IsPlayer != true) return;
            var power = applier.GetPower<DeniaProofOfPersonalityPower>();
            if (power == null) return;

            int bias = TuneStrainState.GetBias(target);
            if (bias <= 0 || target.IsDead) return;

            if (bias < 2)
            {
                await TuneStrainState.TryAddBias(target, 1, applier, source);
            }
            else
            {
                await AemeathMechanicsApi.TriggerUnconditionalResonanceBreak(target, applier, source);
            }
        });
    }
}
