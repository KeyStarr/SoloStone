using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using MiniJSON;
using EngineCard = SabberStoneCore.Model.Card;

// Versioned inputs and stable integer draws keep every offer reproducible across runtimes and save reloads.
public sealed class DraftRules
{
    public static DraftRules Arena(int version = 1) => version == 1 ? arena.Value
        : throw new InvalidOperationException("Unknown Arena rules version.");
    private static readonly Lazy<DraftRules> arena = new Lazy<DraftRules>(() => new DraftRules());
    public int Version { get; }
    public int EntryGold { get; }
    public int DeckSize { get; }
    public int OfferSize { get; }
    public int MaxWins { get; }
    public int MaxLosses { get; }
    private readonly int classWeight;
    private readonly string[] heroes;
    private readonly KeyValuePair<Rarity, int>[] rarities;
    private readonly HashSet<string> denyIds;
    private readonly int[][] rewards;
    // Built on the first offer only: reading the rules (View with no run, validation) never touches the card pool.
    private readonly Lazy<EngineCard[]> pool;

    // Tests only: the same rules over a fixed card list (a tiny pool exercises the offer fallback).
    internal static DraftRules WithPool(IEnumerable<EngineCard> cards) => new DraftRules(cards.ToArray());
    // Called once with a message when an offer had to fall back (the plugin logs it); never throws.
    internal static Action<string> FallbackLog;
    internal static bool fallbackLogged;

    private DraftRules(EngineCard[] poolOverride = null)
    {
        using (var stream = typeof(DraftRules).Assembly.GetManifestResourceStream("SoloStone.Arena.arena-rules-v1.json"))
        {
            var root = EmbeddedJson.Object(EmbeddedJson.Read(stream));
            Version = Required(root, "version"); EntryGold = Required(root, "entryGold");
            DeckSize = Required(root, "deckSize"); OfferSize = Required(root, "offerSize");
            MaxWins = Required(root, "maxWins"); MaxLosses = Required(root, "maxLosses");
            classWeight = Required(root, "classWeight");
            heroes = EmbeddedJson.Array(root["heroes"]).Cast<string>().ToArray();
            denyIds = new HashSet<string>(EmbeddedJson.Array(root["denyIds"]).Cast<string>(), StringComparer.Ordinal);
            rarities = EmbeddedJson.Array(root["rarities"]).Select(value => {
                var row = EmbeddedJson.Object(value);
                return new KeyValuePair<Rarity, int>((Rarity)Enum.Parse(typeof(Rarity), EmbeddedJson.Text(row, "rarity")), Required(row, "weight"));
            }).ToArray();
            rewards = EmbeddedJson.Array(root["rewards"]).Select((value, index) => {
                var row = EmbeddedJson.Object(value);
                if (Required(row, "wins") != index) throw new InvalidOperationException("Invalid Arena reward order.");
                return new[] { Required(row, "packs"), Required(row, "gold"), Required(row, "dust") };
            }).ToArray();
        }
        if (Version != 1 || EntryGold <= 0 || DeckSize <= 0 || OfferSize < 2 || MaxWins <= 0 || MaxLosses <= 0
            || classWeight <= 0 || heroes.Distinct().Count() != heroes.Length || heroes.Length < OfferSize
            || rarities.Length == 0 || rarities.Any(row => row.Value <= 0 || row.Key == Rarity.LEGENDARY)
            || rewards.Length != MaxWins + 1 || rewards.Any(row => row.Any(value => value < 0)))
            throw new InvalidOperationException("Invalid Arena rules.");
        if (poolOverride != null) { pool = new Lazy<EngineCard[]>(() => poolOverride); return; }
        pool = new Lazy<EngineCard[]>(() => LocalCardCatalog.ActivePoolIds().Select(Cards.FromId).Where(LocalCardCatalog.IsStandard).Where(Eligible)
            .OrderBy(card => card.Id, StringComparer.Ordinal).ToArray());
    }

    private static int Required(JsonNode row, string key) =>
        EmbeddedJson.Integer(row, key) ?? throw new InvalidOperationException("Missing Arena rule: " + key);

    public bool Eligible(EngineCard card)
    {
        if (card == null || !card.Collectible || !card.Implemented || denyIds.Contains(card.Id)
            || LocalDecks.FabledBundle(card.Id).Count != 0 || LocalDecks.IsFabledCompanion(card.Id)
            || card[(GameTag)2931] > 0 || card[(GameTag)997] > 0 || card[(GameTag)1048] > 0) return false;
#if ENGINE_SOURCE
        if (SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(card.Id)) return false;
#endif
        string text = card.Text ?? "";
        // Printed deck conditions have no single engine tag (highlander, parity, starting-deck replacements).
        string plain = System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", ""), @"[\s_]+", " ").ToLowerInvariant();
        if (plain.Contains("your deck has") || plain.Contains("your deck contains") || plain.Contains("your deck had")
            || plain.Contains("your starting deck") || plain.Contains("start of game:") || plain.Contains("start of game,")
            || plain.Contains("you started the game with") || plain.Contains("whizbang")) return false;
        return card.Cost <= 10 || !string.IsNullOrWhiteSpace(text);
    }

    internal IReadOnlyList<EngineCard> Pool => pool.Value;
    public IReadOnlyList<string> Heroes => heroes;
    public IReadOnlyList<string> HeroOffers(long seed) => Draw(heroes.ToList(), seed, 0);

    private static EngineCard KnownCard(string id) { try { return Cards.FromId(id); } catch (KeyNotFoundException) { return null; } }

    public IReadOnlyList<string> Offers(long seed, int slot, string hero, IEnumerable<string> deck)
    {
        var ids = deck.ToArray();
        if (!heroes.Contains(hero) || slot < 1 || slot > DeckSize || ids.Length != slot - 1)
            throw new InvalidOperationException("Invalid draft slot or hero.");
        int decision = StableSeed.Derive(seed, Version, slot, Hash(hero), Hash(string.Join("|", ids)));
        var heroClass = LocalDecks.HeroClass(hero);
        // Runes matter for Death Knight only; a drafted card a later card-data update no longer knows adds none instead of throwing.
        var runes = hero == "HERO_11" ? LocalDecks.RuneCounts(ids.Select(KnownCard).Where(card => card != null)) : new int[3];
        var allowed = pool.Value.Where(card => LocalDecks.IsAllowedInDeck(card, heroClass)
            && (hero != "HERO_11" || Enumerable.Range(0, 3).Sum(i => Math.Max(runes[i], card[(GameTag)(2196 + i)])) <= 3)).ToArray();
        Rarity rarity = Rarity.LEGENDARY;
        if (slot != 1)
        {
            int roll = StableSeed.Index(decision, rarities.Sum(row => row.Value));
            foreach (var row in rarities) { roll -= row.Value; if (roll < 0) { rarity = row.Key; break; } }
        }
        var candidates = allowed.Where(card => card.Rarity == rarity || rarity == Rarity.COMMON && card.Rarity == Rarity.FREE).ToList();
        if (candidates.Count < OfferSize) candidates = Fallback(allowed, rarity, slot);
        var result = new List<string>();
        for (int i = 0; i < OfferSize; i++)
        {
            int roll = StableSeed.Index(StableSeed.Derive(decision, i), candidates.Sum(card => ClassWeight(card)));
            var chosen = candidates[0];
            foreach (var card in candidates) { roll -= ClassWeight(card); if (roll < 0) { chosen = card; break; } }
            result.Add(chosen.Id); candidates.Remove(chosen);
        }
        return result.AsReadOnly();
    }

    // A future pool change can leave a rarity bucket (or the legendary first pick) under OfferSize. The offer never fails: it widens
    // deterministically to the other rarities in table order (legendary last, so a later pick gets one only when nothing else fills it),
    // then to every eligible card by id. Only a pool with fewer eligible cards than an offer is unrecoverable.
    private List<EngineCard> Fallback(EngineCard[] allowed, Rarity rarity, int slot)
    {
        var order = new List<Rarity> { rarity };
        order.AddRange(rarities.Select(row => row.Key).Where(key => key != rarity));
        if (!order.Contains(Rarity.LEGENDARY)) order.Add(Rarity.LEGENDARY);
        var result = new List<EngineCard>();
        foreach (var next in order) {
            result.AddRange(allowed.Where(card => (card.Rarity == next || next == Rarity.COMMON && card.Rarity == Rarity.FREE) && !result.Contains(card)));
            if (result.Count >= OfferSize) break;
        }
        if (result.Count < OfferSize) result = allowed.OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
        if (result.Count < OfferSize) throw new InvalidOperationException("Draft pool cannot fill an offer.");
        if (!fallbackLogged) { fallbackLogged = true; FallbackLog?.Invoke("Arena offer for slot " + slot + " fell back: fewer than " + OfferSize + " " + rarity + " cards in the pool."); }
        return result;
    }

    private int ClassWeight(EngineCard card) => card.Class == CardClass.NEUTRAL && card.MultiClassGroup == 0
        && card[(GameTag)476] == 0 ? 1 : classWeight;
    private IReadOnlyList<string> Draw(List<string> choices, long seed, int slot)
    {
        var result = new List<string>();
        for (int i = 0; i < OfferSize; i++) {
            int index = StableSeed.Index(StableSeed.Derive(seed, Version, slot, i), choices.Count);
            result.Add(choices[index]); choices.RemoveAt(index);
        }
        return result.AsReadOnly();
    }
    private static long Hash(string value)
    {
        unchecked { ulong hash = 14695981039346656037UL; foreach (char c in value) { hash ^= c; hash *= 1099511628211UL; } return (long)hash; }
    }

    // The reward for every win count at one pace, for the native rewards list (season reward levels). Preview only: the paid
    // reward is computed at the end of the run with the run's own pack set.
    public IReadOnlyList<ArenaReward> RewardTable(string tierId, string packType)
    {
        string key = tierId + "|" + packType;
        lock (rewardTables)
        {
            if (!rewardTables.TryGetValue(key, out var table))
                rewardTables[key] = table = Enumerable.Range(0, MaxWins + 1).Select(wins => Reward(null, wins, tierId, packType)).ToList().AsReadOnly();
            return table;
        }
    }
    private readonly Dictionary<string, IReadOnlyList<ArenaReward>> rewardTables = new Dictionary<string, IReadOnlyList<ArenaReward>>();
    internal static string PreviewPackType => FairPackRules.PackTypes.Contains(FairPackRules.Standard) ? FairPackRules.Standard : FairPackRules.PackTypes.FirstOrDefault();

    public ArenaReward Reward(string runId, int wins, string tierId, string packType)
    {
        if (wins < 0 || wins > MaxWins || !FairPackRules.PackTypes.Contains(packType))
            throw new InvalidOperationException("Invalid Arena reward inputs.");
        var row = rewards[wins]; var tier = GrindTiers.Find(tierId);
        return new ArenaReward { RunId = runId, Wins = wins, Gold = tier.Gold(row[1]), Dust = tier.Dust(row[2]),
            PackTypes = Enumerable.Repeat(packType, row[0]).ToList() };
    }
}

public sealed class DraftBotResult
{
    public string Hero;
    public List<string> Deck = new List<string>();
    public double Quality;
}

public static class DraftBot
{
    public static DraftBotResult Draft(long seed, double skill)
    {
        if (double.IsNaN(skill) || skill < 0 || skill > 1) throw new ArgumentOutOfRangeException(nameof(skill));
        var rules = DraftRules.Arena();
        var result = new DraftBotResult { Hero = rules.HeroOffers(seed)[0] };
        for (int slot = 1; slot <= rules.DeckSize; slot++)
        {
            var choices = rules.Offers(seed, slot, result.Hero, result.Deck)
                .Select(id => new { Id = id, Score = Score(Cards.FromId(id), result.Deck) })
                .OrderByDescending(row => row.Score).ThenBy(row => row.Id, StringComparer.Ordinal).ToArray();
            int index = StableSeed.Index(StableSeed.Derive(seed, slot, 0x424f54), 10000) < (1 - skill) * 8000 ? 1 : 0;
            result.Deck.Add(choices[index].Id); result.Quality += choices[index].Score;
        }
        result.Quality /= rules.DeckSize;
        return result;
    }

    public static double Score(EngineCard card, IEnumerable<string> deck)
    {
        var cards = deck.Select(Cards.FromId).ToArray();
        int bucket = Math.Min(7, card.Cost);
        double[] target = { 1, 3, 6, 6, 5, 4, 2, 3 };
        double value = card.Type == CardType.MINION ? (card.ATK + card.Health) / (double)(card.Cost + 1)
            : card.Type == CardType.WEAPON ? (card.ATK * card[GameTag.DURABILITY] + 1.0) / (card.Cost + 1) : 1.5 + Math.Min(7, card.Cost) * 0.08;
        string text = (card.Text ?? "").ToLowerInvariant();
        foreach (string keyword in new[] { "draw", "discover", "damage", "destroy", "rush", "taunt", "divine shield", "lifesteal", "summon" })
            if (text.Contains(keyword)) value += 0.35;
        return value - 0.4 * (cards.Count(c => Math.Min(7, c.Cost) == bucket) - target[bucket] * cards.Length / 30.0);
    }
}
