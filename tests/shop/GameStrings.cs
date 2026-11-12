internal static class GameStrings
{
    internal static readonly Dictionary<string, string> Translations = new();
    internal static string Get(string key) => Translations.GetValueOrDefault(key, key);
}
