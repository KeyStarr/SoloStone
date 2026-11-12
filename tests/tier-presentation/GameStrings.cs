// Stand-in for the client's string table, which NativeStrings looks labels up in. It holds none, so every label is the English text.
internal static class GameStrings
{
    internal static readonly Dictionary<string, string> Translations = new();
    internal static string Get(string key) => Translations.GetValueOrDefault(key, key);
}
