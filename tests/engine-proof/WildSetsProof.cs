using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;

internal static class WildSetsProof
{
    private static readonly List<string> Failures = new();
    private static readonly HashSet<string> ProvenIds = new(StringComparer.Ordinal);

    internal static void Case(string id, Action<Game, Controller, string> scenario)
    {
        try
        {
            var card = Cards.FromId(id);
            Core2026Proof.Check(card != null && card.Implemented && CardDefs.Instance.Get.ContainsKey(id)
                && !EffectModules.ByDbfId.ContainsKey(card.AssetId), "scripted legacy definition " + id);
            var policy = OfflinePoolPolicy.Load();
            var active = policy.Metas.Single(meta => meta.Id == policy.ActiveMetaId);
            Core2026Proof.Check(!active.SetIds.Values.Contains((int)card.Set) && !active.ExtraCards.Contains(id),
                "outside active Standard meta " + active.Id);
            Core2026Proof.Check(Cards.IsFormatLegal(card, FormatType.FT_WILD), "collectible Wild deck legality");
            var reprints = Reprints.ByDbfId.Where(pair => pair.Value == card.AssetId)
                .Select(pair => Cards.FromAssetId(pair.Key))
                .Where(reprint => active.SetIds.Values.Contains((int)reprint.Set) || active.ExtraCards.Contains(reprint.Id))
                .Select(reprint => reprint.Id).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Console.WriteLine(id + " shares-behaviour: " + (reprints.Length == 0 ? "none" : string.Join(", ", reprints)));
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = CreateGame(seed, GenerationFormat.Wild);
                scenario(game, game.Player1, id);
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after scenario");
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after scenario");
            }
            Core2026Proof.Check(ProvenIds.Add(id), "one Wild proof per card");
            Console.WriteLine("PASS Wild sets " + id + "; seeds=3; clone/snapshot");
        }
        catch (Exception error) { Failures.Add(id + ": " + error); }
    }

    /// <summary>A proof of a card held back from the manifest because Standard uses it (hold rule, #87/#141): never registered, run only
    /// with WILD_HELD=1, prints PASS or FAIL and never throws. The card's script is at base, so a scenario that states the 37.0
    /// behaviour FAILS until the card is released with a Standard golden audit.</summary>
    internal static void Held(string id, string reason, Action<Game, Controller, string> scenario) { Held(id, scenario, reason); }

    internal static void Held(string id, Action<Game, Controller, string> scenario, string reason = null)
    {
        if (Environment.GetEnvironmentVariable("WILD_HELD") != "1") return;
        try
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = CreateGame(seed, GenerationFormat.Wild);
                scenario(game, game.Player1, id);
            }
            Console.WriteLine("HELD " + id + " PASS (not registered)" + (reason == null ? "" : "; " + reason));
        }
        catch (Exception error) { Console.WriteLine("HELD " + id + " FAIL (not registered" + (reason == null ? ", script at base" : "; " + reason) + "): " + error.Message); }
    }

    internal static Game CreateGame(int seed, GenerationFormat poolFormat)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD,
            PoolFormat = poolFormat });
        game.StartGame();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
            owner.BaseMana = 10;
        }
        return game;
    }

    internal static string[] ManifestIds() => Directory.GetFiles("tests/progression/batches/wild", "*.txt")
        .OrderBy(path => path, StringComparer.Ordinal)
        .SelectMany(path => File.ReadAllText(path).TrimEnd('\r', '\n').Split('\n')).Select(line => line.TrimEnd('\r')).ToArray();

    internal static void CheckCoverage(IReadOnlyCollection<string> proven, IReadOnlyCollection<string> listed)
    {
        Core2026Proof.Check(listed.Count == listed.Distinct(StringComparer.Ordinal).Count(), "duplicate Wild manifest ID");
        var missing = listed.Except(proven, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var unlisted = proven.Except(listed, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Core2026Proof.Check(missing.Length == 0 && unlisted.Length == 0, "Wild proofs run differ from the manifests: missing "
            + (missing.Length == 0 ? "none" : string.Join(",", missing)) + "; unlisted " + (unlisted.Length == 0 ? "none" : string.Join(",", unlisted)));
    }

    // Negative controls for CheckCoverage: a listed card whose Case never ran, and a proven card no manifest lists.
    internal static void CoverageRefuses()
    {
        bool Refused(string[] proven, string[] listed)
        {
            try { CheckCoverage(proven, listed); return false; }
            catch (Exception error) { return error.Message.Contains("differ from the manifests"); }
        }
        Core2026Proof.Check(Refused(new[] { "CS2_028" }, new[] { "CS2_028", "CS2_031" }), "coverage refuses a listed card never proven");
        Core2026Proof.Check(Refused(new[] { "CS2_028", "CS2_031" }, new[] { "CS2_028" }), "coverage refuses an unlisted proven card");
        CheckCoverage(new[] { "CS2_031", "CS2_028" }, new[] { "CS2_028", "CS2_031" });
    }

    internal static void Run(Action selectedProof = null)
    {
        Failures.Clear();
        ProvenIds.Clear();
        if (selectedProof == null) GroupProofs.Run(ProofKind.WildCards);
        else selectedProof();
        Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
        if (selectedProof == null)
        {
            CoverageRefuses();
            Core2026Proof.Check(ProvenIds.Count > 0, "nonempty Wild card proofs");
            CheckCoverage(ProvenIds, ManifestIds());
        }
        Console.WriteLine($"wild-sets: cards={ProvenIds.Count}; seeds=3; PASS");
    }
}
