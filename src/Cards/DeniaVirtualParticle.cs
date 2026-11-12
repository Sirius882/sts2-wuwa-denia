using TuneStrain;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Denia;

/// <summary>虚质粒子 — Uncommon Skill</summary>
[Pool(typeof(DeniaCardPool))]
public sealed class DeniaVirtualParticle : DeniaCard
{
    public override int CurrentDarkCoreCost => 1;
    public override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => "res://images/packed/card_portraits/denia/card_face_virtual_particle.png";

    public DeniaVirtualParticle() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override System.Collections.Generic.List<(string, string)>? Localization => new CardLoc(Title: "虚质粒子",
            Description: "给你当前牌组中所有牌附加集谐响应。\n黯核强化：在弃牌堆里放入一张这张牌的复制品。");

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var card in Owner.PlayerCombatState!.AllCards.Concat(Owner.Deck.Cards).Where(card => !card.HasBeenRemovedFromState).ToArray())
            TuneStrainState.AddTemporaryResponse(Owner, card);
        if (!await TrySpendDarkCore(play)) return;
        var copy = CreateClone();
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Discard, Owner, CardPilePosition.Bottom);
    }

    protected override void OnUpgrade() {
        EnergyCost.UpgradeBy(-1);
    }
}
