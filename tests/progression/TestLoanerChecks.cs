using System.Reflection;

// The decision-path test loaner exists only in source-engine builds.
internal static class TestLoanerChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    internal static void Run(string root)
    {
        // Rollback safety: a test opponent left in history, even one this build lacks, never blocks rotation.
        Check(LocalDecks.NextOpponent(new[] { "mage", "test-cenarius" }).Id == LocalDecks.NextOpponent(new[] { "mage" }).Id
            && LocalDecks.NextOpponent(new[] { "test-cenarius" }).Id == LocalDecks.NextOpponent(new string[0]).Id,
            "unknown or test opponents in history are ignored by rotation");
        LocalDeck loaner = LocalDecks.Get(LocalDecks.TestDecisionsId), opponent = LocalDecks.Get(LocalDecks.TestCenariusId);
        Check(loaner.Build().Count == 30 && opponent.Build().Count == 30, "test loaner and test opponent build");
        Check(LocalDecks.All.All(deck => !deck.Id.StartsWith("test-", StringComparison.Ordinal)),
            "starter archetypes contain no test deck");
        Check(LocalDecks.OpponentFor(LocalDecks.TestDecisionsId, new[] { "mage" }) == opponent
            && LocalDecks.OpponentFor("mage", new string[0]) == LocalDecks.NextOpponent(new string[0]),
            "test loaner faces its fixed opponent; normal loaners keep the rotation");
        string[] histories = { "", "mage", LocalDecks.TestCenariusId, "mage,warrior", LocalDecks.TestCenariusId + ",priest" };
        Check(histories.All(history => !LocalDecks.NextOpponent(history.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Id.StartsWith("test-", StringComparison.Ordinal)), "rotation never offers a test deck, even after a test match");
        string[] ids = loaner.CardIds.ToArray();
        Check(ids.All(id => NativeUIProjection.Supported.Contains(id)) && ids.Count(LocalCoreProgression.IsAvailable) == 14,
            "test loaner uses only supported cards, including fourteen available Core cards");
        NativeUIProjection.ValidateSelection(LocalDecks.TestDecisionsId, loaner.Hero, ids, new Dictionary<string, int>());
        long deckId = NativeUIProjection.DeckId(LocalDecks.TestDecisionsId);
        Check(deckId == 900000010L && NativeUIProjection.IsLoanerId(deckId) && !NativeUIProjection.IsPersonalId(deckId)
            && NativeUIProjection.IsExactLoaner(deckId, loaner.Hero, ids), "test loaner has a reserved non-personal native ID");
        var ranks = NativeUIProjection.ExpectedNativeSortOrders(new[] { new KeyValuePair<long, long>(910000010L, 0) }, NativeUIProjection.ProjectedLoanerKeys(1, "default"));
        Check(ranks.Count == 26 && ranks.Values.Distinct().Count() == ranks.Count && ranks[910000010L] == 0
            && ranks[NativeUIProjection.DeckId(LocalDecks.TestShowcaseId)] == NativeUIProjection.MaximumPersonalDecks + 8
            && ranks[deckId] == NativeUIProjection.MaximumPersonalDecks + 9,
            "native sort order keeps showcase then test loaner in fixed band without collisions");
        Check(LocalDecks.PlayerHeroes.Count == 11 && LocalDecks.PlayerHeroes.Contains("HERO_11"),
            "source supports eleven player classes including Death Knight");
        Check(LocalDecks.PlayerHeroes.All(hero => LocalDecks.All.Concat(LocalDecks.TestLoaners).Any(deck => deck.Hero == hero)),
            "every source player class has an immutable playable loaner");
        Check(NativeUIProjection.ProjectedLoanerKeys(0).Count == 11 && NativeUIProjection.ProjectedLoanerKeys(0, "default").Count == 26,
            "release shows eleven Core starters; explicit harness selector projects twenty-six loaners");
        var nativeIds = LocalDecks.All.Concat(LocalDecks.TestLoaners).Select(deck => NativeUIProjection.DeckId(deck.Id)).ToArray();
        Check(nativeIds.Length > 0 && nativeIds.Distinct().Count() == nativeIds.Length
            && nativeIds.All(id => NativeUIProjection.IsLoanerId(id) && !NativeUIProjection.IsPersonalId(id)),
            "all starter and test native ids are unique and outside personal range (" + nativeIds.Length + " decks)");
        // StandardBatchChecks reads the client card export, so it is the client-tier suite progression-standard-batches.
        RushSilenceChecks.Run(root);
        string path = Path.Combine(root, "test-loaner.xml");
        using (var profile = new LocalProfile(path))
        {
            var match = profile.Begin(LocalDecks.TestDecisionsId, LocalDecks.TestCenariusId, 3);
            Check(match.DeckHero == "HERO_08" && match.Opponent == LocalDecks.TestCenariusId, "test loaner match records Mage XP target");
        }
        using (var reopened = new LocalProfile(path))
            Check(reopened.Pending?.Opponent == LocalDecks.TestCenariusId, "pending test match survives a profile reload");
    }
}
