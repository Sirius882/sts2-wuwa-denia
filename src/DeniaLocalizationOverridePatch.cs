using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyAfter("BaseLib")]
public static class DeniaLocalizationOverridePatch
{
    private static readonly (string Table, string File)[] LocalizationFiles =
    {
        ("characters", "characters.json"),
        ("cards", "cards.json"),
        ("powers", "powers.json"),
        ("relics", "relics.json")
    };

    public static void Postfix()
    {
        string language = LocManager.Instance.Language;
        if (string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach ((string table, string file) in LocalizationFiles)
        {
            Dictionary<string, string>? loc = LoadDeniaLoc(language, file);
            if (loc == null && !string.Equals(language, "eng", StringComparison.OrdinalIgnoreCase))
            {
                loc = LoadDeniaLoc("eng", file);
            }

            if (loc is { Count: > 0 })
            {
                LocManager.Instance.GetTable(table).MergeWith(loc);
            }
        }
    }

    private static Dictionary<string, string>? LoadDeniaLoc(string language, string file)
    {
        string path = $"res://denia/localization/{language}/{file}";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        using FileAccess? fileAccess = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (fileAccess == null)
        {
            return null;
        }

        Dictionary<string, string>? allEntries = JsonSerializer.Deserialize<Dictionary<string, string>>(fileAccess.GetAsText());
        if (allEntries == null)
        {
            return null;
        }

        Dictionary<string, string> deniaEntries = [];
        foreach ((string key, string value) in allEntries)
        {
            if (key.StartsWith("DENIA-", StringComparison.Ordinal))
            {
                deniaEntries[key] = value;
            }
        }

        return deniaEntries;
    }
}