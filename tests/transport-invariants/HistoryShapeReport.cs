using System.Text.Json;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;

namespace TransportInvariants;

internal sealed class HistoryShapeBaseline
{
    public string Rule { get; set; }
    public string Card { get; set; }
    public int? Count { get; set; }
    public string Reason { get; set; }
    public string Kind { get; set; }
    // SPLIT_EFFECT_BLOCK only: the reviewed finding details; any other shape of the same card is new.
    public string[] Shapes { get; set; }
    public HistoryShapeCondition[] Conditions { get; set; }
}
internal sealed class HistoryShapeBucket
{
    public string Rule { get; init; }
    public string Severity { get; init; }
    public string Card { get; init; }
    public int Count { get; set; }
    public SortedSet<string> Cards { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Shapes { get; } = new(StringComparer.Ordinal);
    public HistoryShapeFinding Example { get; init; }
    internal readonly List<HistoryShapeFinding> Findings = new();
}
internal sealed record HistoryShapeGame(string Replay, HistoryShapeFinding[] Findings);
internal sealed record HistoryShapeRaw(int Schema, bool FullCorpus, string[] PlayedCards, HistoryShapeGame[] Games);
internal static class HistoryShapeReport
{
    internal const string BaselinePath = "tests/transport-invariants/history-shape-baseline.json";
    internal const string RawFile = "history-shape-raw.json";
    private static readonly Dictionary<(string Rule, string Card), HistoryShapeBucket> Buckets = new();
    private static readonly Dictionary<(string Card, string Variant), int> SplitEffectBlocks = new();
    private static readonly HashSet<string> Played = new(StringComparer.Ordinal);
    private static readonly List<HistoryShapeGame> Games = new();
    internal static void PlayedCard(string card) => Played.Add(card);
    internal static bool Add(HistoryShape shape)
    {
        shape.Finish();
        return AddGame(new(shape.Replay, shape.Findings.ToArray()));
    }
    private static bool AddGame(HistoryShapeGame game)
    {
        Games.Add(game);
        bool added = false;
        foreach (var finding in game.Findings)
        {
            if (finding.Rule == "SPLIT_EFFECT_BLOCK")
            {
                var splitKey = (finding.Card, finding.Detail);
                SplitEffectBlocks[splitKey] = SplitEffectBlocks.GetValueOrDefault(splitKey) + 1;
            }
            var key = (finding.Rule, finding.Card);
            if (!Buckets.TryGetValue(key, out var bucket))
            {
                added = true;
                Buckets.Add(key, bucket = new() { Rule = finding.Rule, Severity = finding.Severity, Card = finding.Card, Example = finding });
            }
            bucket.Count++; bucket.Findings.Add(finding);
            if (finding.Rule == "SPLIT_EFFECT_BLOCK") bucket.Shapes.Add(finding.Detail);
            foreach (string card in new[] { finding.Card, finding.BlockCard, finding.ActualCard, finding.VictimCard })
                if (!string.IsNullOrEmpty(card) && card != "<game>") bucket.Cards.Add(card);
        }
        return added;
    }
    internal static bool Reclassify(string path)
    {
        var raw = JsonSerializer.Deserialize<HistoryShapeRaw>(File.ReadAllText(path), Program.Json);
        if (raw?.Schema != 1 || raw.PlayedCards == null || raw.Games == null
            || raw.PlayedCards.Any(string.IsNullOrWhiteSpace)
            || raw.Games.Any(g => g == null || string.IsNullOrWhiteSpace(g.Replay) || g.Findings == null
                || g.Findings.Any(f => f == null || !HistoryShape.Rules.Contains(f.Rule)
                    || f.Severity != (HistoryShape.InfoRules.Contains(f.Rule) ? "info" : "error")
                    || string.IsNullOrWhiteSpace(f.Card) || f.Detail == null)))
            throw new InvalidOperationException("invalid raw history-shape evidence");
        HydrateConditions(raw);
        Buckets.Clear(); SplitEffectBlocks.Clear(); Played.Clear(); Games.Clear();
        foreach (string card in raw.PlayedCards) Played.Add(card);
        foreach (var game in raw.Games) AddGame(game);
        return Write(raw.FullCorpus);
    }
    // Schema-1 evidence predates typed conditions. Re-run only affected deterministic games and
    // correlate every original finding exactly; never infer acceptance from its diagnostic prose.
    private static void HydrateConditions(HistoryShapeRaw raw)
    {
        var constrained = LoadBaseline().Where(e => e.Conditions != null).Select(e => (e.Rule, e.Card)).ToHashSet();
        bool Needs(HistoryShapeFinding f) => f.Conditions == null && constrained.Contains((f.Rule, f.Card));
        var needed = raw.Games.SelectMany(g => g.Findings).Where(Needs).Select(f => f.Replay).ToHashSet(StringComparer.Ordinal);
        if (needed.Count == 0) return;
        TransportHooks.Install();
        Buckets.Clear(); SplitEffectBlocks.Clear(); Played.Clear(); Games.Clear();
        var runner = new Runner();
        foreach (var replay in Runner.Cases("standard-batch-1", 9).Concat(Runner.Fuzz()))
        {
            string path = $"{Runner.Output}/{replay.Game.Id}.json";
            if (!needed.Contains(path)) continue;
            runner.Run(replay);
            needed.Remove(path);
        }
        if (needed.Count != 0 || runner.Failures != 0 || runner.ViolationCount != 0)
            throw new InvalidOperationException("cannot recover history conditions: " + string.Join(",", needed));
        var observed = Games.SelectMany(g => g.Findings).ToLookup(f => f with { Conditions = null });
        int recovered = 0;
        foreach (var game in raw.Games)
            for (int i = 0; i < game.Findings.Length; i++)
            {
                var original = game.Findings[i];
                if (!Needs(original)) continue;
                var matches = observed[original].ToArray();
                if (matches.Length == 0 || matches.Any(f => !f.Conditions.SequenceEqual(matches[0].Conditions)))
                    throw new InvalidOperationException($"history evidence changed: {original.Replay}@{original.Step} {original.Rule}/{original.Card}");
                game.Findings[i] = matches[0]; recovered++;
            }
        Console.WriteLine($"history-shape recovered typed conditions for {recovered} original findings; targeted games={runner.Completed.Values.Sum()}");
    }
    internal static HistoryShapeBaseline[] LoadBaseline() => JsonSerializer.Deserialize<HistoryShapeBaseline[]>(File.ReadAllText(BaselinePath), Program.Json)
        ?? throw new InvalidOperationException("history-shape baseline must be an array");
    internal static Dictionary<(string Rule, string Card), HistoryShapeBaseline> Baseline(HistoryShapeBaseline[] baseline)
    {
        var allowed = new Dictionary<(string Rule, string Card), HistoryShapeBaseline>();
        foreach (var entry in baseline)
        {
            bool split = entry.Rule == "SPLIT_EFFECT_BLOCK";
            if (!HistoryShape.Rules.Contains(entry.Rule) || HistoryShape.InfoRules.Contains(entry.Rule)
                || string.IsNullOrWhiteSpace(entry.Card) || entry.Kind is not ("defect" or "accepted")
                || string.IsNullOrWhiteSpace(entry.Reason) || entry.Count < 0
                || entry.Conditions != null && (entry.Conditions.Length == 0 || entry.Conditions.Any(c => !Enum.IsDefined(c))
                    || entry.Conditions.Distinct().Count() != entry.Conditions.Length) || !allowed.TryAdd((entry.Rule, entry.Card), entry)
                || split != (entry.Shapes != null) || split && (entry.Shapes.Length == 0
                    || entry.Shapes.Any(string.IsNullOrWhiteSpace) || entry.Shapes.Distinct(StringComparer.Ordinal).Count() != entry.Shapes.Length))
                throw new InvalidOperationException("invalid/duplicate history-shape baseline entry: " + entry.Rule + "/" + entry.Card);
        }
        return allowed;
    }
    // Returns the bucket's unreviewed shapes; "" stands for a bucket with no baseline entry at all.
    internal static string[] Unreviewed(HistoryShapeBucket bucket, HistoryShapeBaseline entry)
    {
        if (entry == null) return new[] { "" };
        var unreviewed = entry.Shapes == null ? Array.Empty<string>() : bucket.Shapes.Except(entry.Shapes, StringComparer.Ordinal);
        if (entry.Conditions != null)
            unreviewed = unreviewed.Concat(bucket.Findings.Count == 0 ? new[] { "missing typed conditions" }
                : bucket.Findings.Where(f => f.Conditions == null || !f.Conditions.Intersect(entry.Conditions).Any()).Select(f => f.Detail));
        return unreviewed.Distinct(StringComparer.Ordinal).ToArray();
    }
    // New error shapes fail; unseen reviewed shapes are full-corpus diagnostics only.
    internal static (HistoryShapeBucket[] Fresh, HistoryShapeBaseline[] Stale) Classify(IReadOnlyCollection<HistoryShapeBucket> buckets,
        HistoryShapeBaseline[] baseline, bool fullCorpus)
    {
        var allowed = Baseline(baseline);
        var fresh = buckets.Where(b => b.Severity == "error" && Unreviewed(b, allowed.GetValueOrDefault((b.Rule, b.Card))).Length > 0).ToArray();
        var seen = buckets.ToDictionary(b => (b.Rule, b.Card));
        var stale = fullCorpus ? baseline.Where(e => !seen.TryGetValue((e.Rule, e.Card), out var b)
            || e.Shapes != null && e.Shapes.Except(b.Shapes, StringComparer.Ordinal).Any()).ToArray() : Array.Empty<HistoryShapeBaseline>();
        return (fresh, stale);
    }
    internal static HistoryShapeBucket Bucket(IEnumerable<HistoryShapeFinding> findings, string rule, string card)
    {
        var bucket = new HistoryShapeBucket { Rule = rule, Severity = "error", Card = card };
        foreach (var finding in findings.Where(f => f.Rule == rule && f.Card == card)) { bucket.Count++; bucket.Findings.Add(finding); bucket.Shapes.Add(finding.Detail); }
        return bucket;
    }
    internal static bool Write(bool fullCorpus)
    {
        Program.Save($"{Runner.Output}/{RawFile}", new HistoryShapeRaw(1, fullCorpus,
            Played.Order(StringComparer.Ordinal).ToArray(), Games.ToArray()));
        var baseline = LoadBaseline(); var allowed = Baseline(baseline);
        var buckets = Buckets.Values.OrderByDescending(b => b.Count).ThenBy(b => b.Rule, StringComparer.Ordinal)
            .ThenBy(b => b.Card, StringComparer.Ordinal).ToArray();
        var errors = buckets.Where(b => b.Severity == "error").ToArray();
        string[] Unseen(HistoryShapeBucket b) => Unreviewed(b, allowed.GetValueOrDefault((b.Rule, b.Card)));
        var (fresh, stale) = Classify(buckets, baseline, fullCorpus);
        var registered = StandardSets.CollectibleIds.Concat(RebalancedCore2026.ByDbfId.Keys.Select(Cards.FromAssetId).Select(c => c.Id))
            .Concat(Reprints.ByDbfId.Keys.Select(Cards.FromAssetId).Select(c => c.Id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var uncovered = registered.Except(Played, StringComparer.Ordinal).ToArray();
        Program.Save($"{Runner.Output}/history-shape.json", new { schema = 1, fullCorpus,
            errors = errors.Sum(b => b.Count), info = buckets.Where(b => b.Severity == "info").Sum(b => b.Count),
            baselined = errors.Length - fresh.Length, @new = fresh.Length, stale,
            uncoveredRegisteredCards = uncovered,
            registeredCards = registered.Select(card => new { card, played = Played.Contains(card),
                rules = buckets.Where(b => b.Card == card).Select(b => b.Rule).Distinct().Order(StringComparer.Ordinal).ToArray() }).ToArray(),
            buckets });
        Console.WriteLine($"history-shape: errors={errors.Sum(b => b.Count)} info={buckets.Where(b => b.Severity == "info").Sum(b => b.Count)} buckets={buckets.Length} baselined={errors.Length - fresh.Length} new={fresh.Length} stale={stale.Length} cards={registered.Length}");
        foreach (var bucket in fresh.Take(15)) Console.Error.WriteLine($"history-shape NEW {bucket.Rule}/{bucket.Card} count={bucket.Count} {bucket.Example.Replay}@{bucket.Example.Step}"
            + string.Concat(Unseen(bucket).Where(s => s.Length > 0).Select(s => $" shape=[{s}]")));
        foreach (var entry in stale.Take(15)) Console.Error.WriteLine($"history-shape STALE {entry.Rule}/{entry.Card}"
            + (entry.Shapes == null || !Buckets.TryGetValue((entry.Rule, entry.Card), out var seen) ? ""
                : string.Concat(entry.Shapes.Except(seen.Shapes, StringComparer.Ordinal).Select(s => $" shape=[{s}]"))));
        Program.Save($"{Runner.Output}/split-effect-blocks.json", new { schema = 1, fullCorpus,
            occurrences = SplitEffectBlocks.Values.Sum(),
            cards = SplitEffectBlocks.Keys.Select(k => k.Card).Distinct(StringComparer.Ordinal).Count(),
            variants = SplitEffectBlocks.OrderBy(p => p.Key.Card, StringComparer.Ordinal).ThenBy(p => p.Key.Variant, StringComparer.Ordinal)
                .Select(p => new { card = p.Key.Card, variant = p.Key.Variant, count = p.Value }).ToArray() });
        return fresh.Length == 0;
    }
}
