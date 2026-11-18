using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using static Core2026Proof;

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
            var definitions = ClientExport.TryCards();
            if (definitions != null)
            {
                Core2026Proof.Check(definitions.TryGetValue(id, out var data), "pinned cardId missing: " + id);
                Core2026Proof.Check(card.Id == data.GetProperty("cardId").GetString(), "pinned cardId");
                Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32(), "pinned dbfId");
                Core2026Proof.Check(card.Cost == data.GetProperty("cost").GetInt32(), "pinned cost");
                Core2026Proof.Check(card.ATK == data.GetProperty("atk").GetInt32(), "pinned attack");
                Core2026Proof.Check(card.Health == data.GetProperty("health").GetInt32(), "pinned health");
                Core2026Proof.Check(card.Text == data.GetProperty("text").GetString(), "pinned text");
                foreach (var tag in data.GetProperty("tags").EnumerateObject())
                {
                    var gameTag = (GameTag)int.Parse(tag.Name);
                    // The client stores Spell Damage and Overload as a flag (1); the engine's value is the amount the text states.
                    int? amount = CardFactsProof.TextAmount(gameTag, data.GetProperty("text").GetString());
                    Core2026Proof.Check(card.Tags.GetValueOrDefault(gameTag) == (CardFactsProof.AmountTags.Contains(gameTag) ? amount : tag.Value.GetInt32()),
                        "pinned tag " + tag.Name);
                }
            }
            else ClientExport.Skip("Wild card identity, stats, text and tags against the client export");
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
                game.StartGame();
                foreach (var owner in new[] { game.Player1, game.Player2 })
                {
                    foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
                    owner.BaseMana = 10;
                }
                scenario(game, game.Player1, id);
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after scenario");
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after scenario");
            }
            Core2026Proof.Check(ProvenIds.Add(id), "one Wild proof per card");
            Console.WriteLine("PASS Wild sets " + id + "; seeds=3; clone/snapshot");
        }
        catch (Exception error) { Failures.Add(id + ": " + error); }
    }

    internal static string[] ManifestIds() => Directory.GetFiles("tests/progression/batches/wild", "*.txt")
        .OrderBy(path => path, StringComparer.Ordinal)
        .SelectMany(path => File.ReadAllText(path).TrimEnd('\r', '\n').Split('\n')).Select(line => line.TrimEnd('\r')).ToArray();

    internal static void CheckCoverage(IReadOnlyCollection<string> proven, IReadOnlyCollection<string> listed)
    {
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
        if (selectedProof != null && ProvenIds.Count == 0)
        {
            var group = (GroupProofAttribute)Attribute.GetCustomAttribute(selectedProof.Method, typeof(GroupProofAttribute));
            string manifest = "tests/progression/batches/wild/" + group.Name.Replace("-cards", "") + ".txt";
            Core2026Proof.Check(!File.Exists(manifest) || string.IsNullOrWhiteSpace(File.ReadAllText(manifest)),
                "empty Wild group still has listed cards: " + group.Name);
            Console.WriteLine($"group {group.Name}: 0 listed cards (all held)");
            return;
        }
        Core2026Proof.Check(ProvenIds.Count > 0, "nonempty Wild card proofs");
        // The manifest check is textual; at run time every listed card (each has an identity row) must have been proven.
        if (selectedProof == null) { CoverageRefuses(); CheckCoverage(ProvenIds, ManifestIds()); }
        Console.WriteLine($"wild-sets: cards={ProvenIds.Count}; seeds=3; PASS");
    }
}
