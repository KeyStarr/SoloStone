// Stand-in for the client's string table: every key is missing, so NativeStrings falls back to English.
internal static class GameStrings
{
    internal static string Get(string key) => key;
}
