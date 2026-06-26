using System.Collections.Generic;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

public sealed class DeniaPotionPool : CustomPotionPoolModel
{
    public override string EnergyColorName => "denia";
    public override Color LabOutlineColor => new Color("FF69B4");

    protected override IEnumerable<PotionModel> GenerateAllPotions() => [];
}
