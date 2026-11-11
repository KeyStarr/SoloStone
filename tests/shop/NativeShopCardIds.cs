// Stand-in for the client's GameUtils card id translation (production resolves it through the game at run time).
// Card ids come from the public card file. Cards outside the active meta's file (hero skins, pet forms) get a stable
// made-up DBF id: the tests only need distinct ids, and none of them pins a real one.
internal static class GameUtils
{
    private static readonly Lazy<Dictionary<string, int>> ids = new(() => PublicFacts.Cards.Value.ToDictionary(card => card.Key, card => card.Value.Dbf));
    private static readonly Dictionary<string, int> madeUp = new();
    internal static readonly HashSet<string> MissingTranslations = new();
    internal static readonly HashSet<int> MissingHeroes = new();
    internal static object GetCardHeroRecordForCardId(int id) => id > 0 && !MissingHeroes.Contains(id) ? new object() : null;
    internal static string TranslateDbIdToCardId(int id) =>
        ids.Value.FirstOrDefault(pair => pair.Value == id).Key ?? madeUp.FirstOrDefault(pair => pair.Value == id).Key;
    internal static int TranslateCardIdToDbId(string cardId)
    {
        if (MissingTranslations.Contains(cardId)) return 0;
        if (ids.Value.TryGetValue(cardId, out int id) || madeUp.TryGetValue(cardId, out id)) return id;
        return madeUp[cardId] = 9_000_000 - madeUp.Count - 1;
    }
}
