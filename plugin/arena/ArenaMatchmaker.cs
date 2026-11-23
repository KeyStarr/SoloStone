using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Arena (#6). Opponents of a run: an NPC plays a deck that was drafted by the same offer rules the player drafted under
// (data/npc/arena-decks-v1.json, made by the draft bot with fixed seeds). Pure and deterministic per
// (run seed, match index, record, the run's earlier opponents). Inputs are the run's record and its own opponent history only:
// never the player's deck, hand, collection or ladder state (no counter-picking, no rigging of the result).
public sealed class ArenaRequest
{
    public long RunSeed { get; }
    public int MatchIndex { get; }                       // matches already played in this run (ties included)
    public int Wins { get; }
    public int Losses { get; }
    public IReadOnlyList<string> UsedDeckIds { get; }    // this run's earlier opponent decks
    public IReadOnlyList<string> UsedNpcIds { get; }     // this run's earlier opponents
    public ArenaRequest(long runSeed, int matchIndex, int wins, int losses, IEnumerable<string> usedDeckIds, IEnumerable<string> usedNpcIds)
    {
        RunSeed = runSeed; MatchIndex = matchIndex; Wins = wins; Losses = losses;
        UsedDeckIds = GoldenData.Copy(usedDeckIds ?? new string[0]); UsedNpcIds = GoldenData.Copy(usedNpcIds ?? new string[0]);
    }
}

// Arena deck catalog: data/npc/arena-decks-v1.json. Deck rows have the same shape as decks-v1.json (cards as {id, count}, counts
// are not limited: a draft has no copy limit), every deck has admission "arena" so no ladder, Casual or inference pool ever sees
// it, plus the draft's provenance: rulesVersion, seed, skill and quality (the drafter's mean pick score).
public sealed class ArenaDeckCatalog
{
    public const string ResourceName = "SoloStone.Npc.arena-decks-v1.json";
    public const string Admission = "arena";
    public const int BucketCount = 5;
    public NpcDeckLibrary Library { get; }
    public IReadOnlyDictionary<string, double> Quality { get; }
    // Decks by quality, lowest first, in BucketCount near-equal buckets (ties by id): bucket 0 = weakest drafts.
    public IReadOnlyList<IReadOnlyList<NpcDeckEntry>> Buckets { get; }

    public ArenaDeckCatalog(NpcDeckLibrary library, IDictionary<string, double> quality)
    {
        Library = library ?? throw new ArgumentNullException(nameof(library));
        if (library.Decks.Count < BucketCount) throw new InvalidDataException("The Arena deck catalog is too small.");
        Quality = new Dictionary<string, double>(quality ?? throw new ArgumentNullException(nameof(quality)), StringComparer.Ordinal);
        foreach (var deck in library.Decks)
        {
            if (deck.Admission != Admission) throw new InvalidDataException("Arena deck must have admission \"arena\": " + deck.Id);
            if (!deck.Id.StartsWith("npc-arena-", StringComparison.Ordinal)) throw new InvalidDataException("Arena deck id must start with npc-arena-: " + deck.Id);
            if (deck.Cards.Sum(card => card.Count) != 30 || deck.Cards.Any(card => card.Count < 1)) throw new InvalidDataException("Arena deck must have 30 cards: " + deck.Id);
            if (!Quality.ContainsKey(deck.Id) || double.IsNaN(Quality[deck.Id]) || double.IsInfinity(Quality[deck.Id]))
                throw new InvalidDataException("Arena deck has no quality score: " + deck.Id);
        }
        var ordered = library.Decks.OrderBy(deck => Quality[deck.Id]).ThenBy(deck => deck.Id, StringComparer.Ordinal).ToList();
        var buckets = new List<IReadOnlyList<NpcDeckEntry>>();
        for (int b = 0; b < BucketCount; b++)
            buckets.Add(ordered.Skip(ordered.Count * b / BucketCount).Take(ordered.Count * (b + 1) / BucketCount - ordered.Count * b / BucketCount).ToArray());
        Buckets = buckets;
    }

    public IReadOnlyList<NpcDeckEntry> Decks => Library.Decks;
    public int BucketOf(string deckId)
    {
        for (int b = 0; b < Buckets.Count; b++) if (Buckets[b].Any(deck => deck.Id == deckId)) return b;
        return -1;
    }

    public static ArenaDeckCatalog Parse(string json) => Load(NpcWorldJson.Parse(json));
    public static ArenaDeckCatalog Load(object root)
    {
        var library = NpcDeckLibrary.Load(root);
        var quality = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (object value in NpcWorldJson.List(NpcWorldJson.Object(root), "decks"))
        {
            var row = NpcWorldJson.Object(value);
            quality[NpcWorldJson.Text(row, "id")] = NpcWorldJson.Number(row, "quality");
        }
        return new ArenaDeckCatalog(library, quality);
    }

    // The library plus these decks, one lookup for plans and LocalDecks.Get (served only while an encounter references them).
    // Ladder generation, matchmakers and deck inference read admitted decks only, so these change no pool. An id that collides
    // with an existing deck is left out (never replaces it).
    public NpcDeckLibrary MergeInto(NpcDeckLibrary other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        return new NpcDeckLibrary(other.Schema, other.LibraryVersion, other.ActiveMeta,
            other.Decks.Concat(Library.Decks.Where(d => other.Find(d.Id) == null)));
    }

    // The catalog restricted to the decks keep accepts; null when too few remain (the caller then plays no Arena matches).
    public ArenaDeckCatalog Keep(Func<NpcDeckEntry, bool> keep)
    {
        var decks = Library.Decks.Where(keep).ToList();
        var kept = new HashSet<string>(decks.Select(d => d.Id), StringComparer.Ordinal);
        return new ArenaDeckCatalog(new NpcDeckLibrary(Library.Schema, Library.LibraryVersion, Library.ActiveMeta, decks),
            Quality.Where(pair => kept.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }
}

public sealed class ArenaQueueRules
{
    // Target quality bucket of a run record: the middle bucket at 0-0, one half bucket up per net win, down per net loss.
    public double MiddleBucket { get; set; } = 2;
    public double BucketPerNetWin { get; set; } = 0.5;
    public double BucketSigma { get; set; } = 0.9;
    // NPCs: the Casual opponent skill scale (an NPC plays at its own rating's tier); Arena has no rating of its own, so the
    // field is centred on the middle of the ladder and never moves with the player's record or results.
    public double NpcTargetRating { get; set; } = 1300;
    public double NpcSigma { get; set; } = 150;
    public double NotableWeight { get; set; } = 1.5;
    public static ArenaQueueRules Default => new ArenaQueueRules();
}

public sealed class ArenaMatchmaker
{
    private const long ArenaTag = 0x41524e41;   // "ARNA"
    private const int StreamBucket = 1, StreamDeck = 2, StreamNpc = 3, StreamMatch = 4;
    public NpcWorld World { get; }
    public ArenaDeckCatalog Catalog { get; }
    public ArenaQueueRules Rules { get; }
    private readonly NpcProfileBuilder buildProfile;

    public ArenaMatchmaker(NpcWorld world, ArenaDeckCatalog catalog, ArenaQueueRules rules = null, NpcProfileBuilder profileBuilder = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Rules = rules ?? ArenaQueueRules.Default;
        buildProfile = profileBuilder ?? NpcEncounterProfiles.Build;
    }

    // Weight of each quality bucket for a record (before the used-deck filter).
    public double[] BucketWeights(int wins, int losses)
    {
        double centre = Math.Max(0, Math.Min(ArenaDeckCatalog.BucketCount - 1, Rules.MiddleBucket + (wins - losses) * Rules.BucketPerNetWin));
        var weights = new double[ArenaDeckCatalog.BucketCount];
        for (int b = 0; b < weights.Length; b++)
        {
            double z = (b - centre) / Rules.BucketSigma;
            weights[b] = Math.Exp(-0.5 * z * z);
        }
        return weights;
    }

    public NpcEncounter Next(ArenaRequest request) => Choose(request);

    public NpcEncounter Choose(ArenaRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.MatchIndex < 0 || request.Wins < 0 || request.Losses < 0) throw new ArgumentException("Invalid Arena request.");
        int seed = StableSeed.Derive(request.RunSeed, ArenaTag, request.MatchIndex);
        var usedDecks = new HashSet<string>(request.UsedDeckIds, StringComparer.Ordinal);
        var usedNpcs = new HashSet<string>(request.UsedNpcIds, StringComparer.Ordinal);

        var free = Catalog.Buckets.Select(bucket => bucket.Where(candidate => !usedDecks.Contains(candidate.Id)).ToArray()).ToArray();
        var weights = BucketWeights(request.Wins, request.Losses);
        for (int b = 0; b < weights.Length; b++) if (free[b].Length == 0) weights[b] = 0;
        if (weights.All(w => w <= 0))
        {
            // Every deck was met (not reachable in one run): repeat rather than refuse.
            free = Catalog.Buckets.Select(bucket => bucket.ToArray()).ToArray();
            weights = BucketWeights(request.Wins, request.Losses);
        }
        int bucketIndex = new MatchRng(seed, StreamBucket).Pick(weights);
        var deck = free[bucketIndex][new MatchRng(seed, StreamDeck).Pick(Enumerable.Repeat(1.0, free[bucketIndex].Length).ToArray())];

        var npcs = World.All.Where(npc => !usedNpcs.Contains(npc.Id)).ToList();
        if (npcs.Count == 0) npcs = World.All.ToList();
        if (npcs.Count == 0) throw new InvalidOperationException("The NPC world has no Arena opponent.");
        var npcWeights = npcs.Select(npc =>
        {
            double z = (npc.HomeRating - Rules.NpcTargetRating) / Rules.NpcSigma;
            return Math.Exp(-0.5 * z * z) * (npc.Notable ? Rules.NotableWeight : 1);
        }).ToArray();
        var chosen = npcs[new MatchRng(seed, StreamNpc).Pick(npcWeights)];

        var skill = chosen.SkillId != null ? World.Catalogs.Skills.Find(chosen.SkillId)
            : NpcPopulationRules.SkillFor(World.Catalogs.Skills, chosen.HomeRating);
        var personality = World.Catalogs.Personalities.Find(chosen.PersonalityId);
        if (skill == null || personality == null)
            throw new InvalidOperationException("NPC " + chosen.Id + " references an unknown skill or personality.");
        var profile = buildProfile(chosen, deck, personality, skill);
        int matchSeed = StableSeed.Derive(seed, StreamMatch) & int.MaxValue;
        return new NpcEncounter(chosen, deck, skill, personality, profile, chosen.HomeRating, matchSeed, "arena");
    }
}
