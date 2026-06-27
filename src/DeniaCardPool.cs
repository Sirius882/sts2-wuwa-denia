using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Denia;

public sealed class DeniaCardPool : CustomCardPoolModel
{
    public override string Title => "denia";
    public override string EnergyColorName => "denia";
    public override string? TextEnergyIconPath => "res://images/packed/sprite_fonts/denia_energy_icon.png";
    public override Color DeckEntryCardColor => new Color("FF69B4");
    public override bool IsColorless => false;
}
