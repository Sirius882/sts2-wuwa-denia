using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Denia;

public sealed class DeniaCardPool : CustomCardPoolModel
{
    private static readonly Texture2D BlackAttackFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_attack.png");
    private static readonly Texture2D BlackSkillFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_skill.png");
    private static readonly Texture2D BlackPowerFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_power.png");
    private static readonly Texture2D PinkAttackFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_pink_card_frame_attack.png");
    private static readonly Texture2D PinkSkillFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_pink_card_frame_skill.png");
    private static readonly Texture2D PinkPowerFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_pink_card_frame_power.png");

    public override string Title => "denia";
    public override string EnergyColorName => "denia";
    public override string? TextEnergyIconPath => "res://images/packed/sprite_fonts/denia_energy_icon.png";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color ShaderColor => Colors.White;
    public override Color DeckEntryCardColor => new Color("FF69B4");
    public override bool IsColorless => false;

    public override Texture2D? CustomFrame(CustomCardModel card)
    {
        bool isBlack = card.IsMutable && card.Owner?.Creature is { } creature && DeniaFormHelper.IsBlack(creature);
        return (isBlack, card.Type) switch
        {
            (true, CardType.Attack) => BlackAttackFrame,
            (true, CardType.Power) => BlackPowerFrame,
            (true, _) => BlackSkillFrame,
            (false, CardType.Attack) => PinkAttackFrame,
            (false, CardType.Power) => PinkPowerFrame,
            _ => PinkSkillFrame,
        };
    }
}
