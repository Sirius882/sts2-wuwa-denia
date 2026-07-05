using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Denia;

public sealed class DeniaCardPool : CustomCardPoolModel
{
    private static readonly Texture2D AttackFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_attack.png");
    private static readonly Texture2D SkillFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_skill.png");
    private static readonly Texture2D PowerFrame = ResourceLoader.Load<Texture2D>("res://images/ui/cards/denia_card_frame_power.png");

    public override string Title => "denia";
    public override string EnergyColorName => "denia";
    public override string? TextEnergyIconPath => "res://images/packed/sprite_fonts/denia_energy_icon.png";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color ShaderColor => Colors.White;
    public override Color DeckEntryCardColor => new Color("FF69B4");
    public override bool IsColorless => false;

    public override Texture2D? CustomFrame(CustomCardModel card)
    {
        return card.Type switch
        {
            CardType.Attack => AttackFrame,
            CardType.Power => PowerFrame,
            _ => SkillFrame,
        };
    }
}
