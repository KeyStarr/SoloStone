internal static class NativeStringsTests
{
    internal static void Run()
    {
        const string key = "GLUE_PROGRESSION_ACHIEVEMENTS_TITLE", english = "Feats";
        foreach (string missing in new[] { null, "", " ", "\t\r\n", "\u00a0", key })
            Check(NativeStrings.Resolve(key, missing, english) == english, "missing string falls back to exact English");
        foreach (string value in new[] { english, "[xx] Feats", "  [xx] Feats  ", key.ToLowerInvariant() })
            Check(NativeStrings.Resolve(key, value, english) == value, "valid string is returned unchanged");
        Check(NativeStrings.Get(key, english) == english, "lookup before strings are ready falls back");
        try
        {
            GameStrings.Translations[key] = "[xx] Feats";
            Check(NativeStrings.Get(key, english) == "[xx] Feats", "later lookup uses ready strings, not a cached fallback");
        }
        finally { GameStrings.Translations.Clear(); }
        Console.WriteLine("PASS native strings fallback and late localization");
    }

    private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
}
