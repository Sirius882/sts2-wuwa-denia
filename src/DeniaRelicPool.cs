using BaseLib.Abstracts;
using Godot;

namespace Denia;

public sealed class DeniaRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "necrobinder";
    public override Color LabOutlineColor => new Color("FF69B4");
    // 遗物通过 [Pool(typeof(DeniaRelicPool))] 特性自动收集
}
