using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Denia;

/// <summary>达妮娅自定义关键词注册。</summary>
public static class DeniaSpecialKeywords
{
    [CustomEnum("RESONANCE_RESPONSE")]
    [KeywordProperties(AutoKeywordPosition.Before)]
    public static CardKeyword ResonanceResponse = CardKeyword.None;
}
