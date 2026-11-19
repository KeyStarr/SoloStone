using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.RandomPools;

// #141: the legacy random tasks read the match's offline pool (GameConfig.PoolFormat), not the frozen V1 lists.
// This proof pins the accessor itself: membership against the manifest, current-value cost buckets, the Wild hook,
// legacy identity, per-manifest isolation and the policy surviving clone and snapshot.
internal static class MatchRandomPoolProof
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("match random pool: " + message);
    }

    private static Game NewGame(int seed = 5, GenerationFormat format = GenerationFormat.Standard,
        FormatType formatType = FormatType.FT_WILD, bool legacy = false, bool unversioned = false,
        OfflinePoolManifest pools = null, CardClass hero = CardClass.MAGE)
    {
        var config = new GameConfig {
            RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true, Logging = false, History = true,
            FormatType = formatType, PoolFormat = format, LegacyPoolReplay = legacy, UnversionedRandomPoolReplay = unversioned,
            ReplayPools = pools, EnableCorpses = hero == CardClass.DEATHKNIGHT,
            Player1HeroClass = hero, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList() };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static readonly CardClass[] Classes = { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN,
        CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR,
        CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT };

    internal static void Run()
    {
        Membership();
        CostBuckets();
        WildHook();
        LegacyIdentity();
        Isolation();
        PolicySurvives();
        ReplayGate();
        Console.WriteLine("PASS match random pools: Standard membership and current values, current-cost buckets, Wild hook, " +
            "legacy list identity, per-manifest isolation, clone/snapshot policy, record version gate");
    }

    private static void Membership()
    {
        var game = NewGame();
        var manifest = game.OfflinePools;
        var standardIds = manifest.Standard.Select(m => m.CardId).ToHashSet();
        foreach (var dest in new[] { PoolDestination.Hand, PoolDestination.Deck, PoolDestination.Summon, PoolDestination.Transform, PoolDestination.Other })
        {
            var pool = Cards.RandomPool(game, dest);
            Check(!pool.Legacy, "a normal game must not take the legacy branch");
            Check(pool.All.Count > 1000, "pool size " + pool.All.Count);
            Check(pool.All.All(c => standardIds.Contains(c.Id)), dest + ": every pool card is a manifest Standard member");
            Check(pool.All.All(c => c.Type != CardType.HERO && c.Implemented), dest + ": no heroes, all implemented");
            Check(pool.All.Select(c => c.Id).Distinct().Count() == pool.All.Count, dest + ": no duplicates");
            bool colossalBody = pool.All.Any(c => c[(GameTag)2247] > 0);
            bool direct = dest == PoolDestination.Hand || dest == PoolDestination.Deck;
            Check(direct || !colossalBody, dest + ": Colossal bodies are never summoned or transformed into");
            Check(pool.All.All(c => c[(GameTag)4538] == 0 || c[(GameTag)2247] == 0), dest + ": no Herald Colossal");
            Check(pool.All.All(c => c[(GameTag)4754] == 0), dest + ": no Old God");
            Check(!pool.All.Any(c => c[GameTag.COST_BLOOD] + c[GameTag.COST_FROST] + c[GameTag.COST_UNHOLY] >= 3), dest + ": no three-rune cards");
        }
        // The current engine card must agree with the pinned member for everything the tasks filter on.
        var byId = manifest.Standard.ToDictionary(m => m.CardId);
        var races = new[] { Race.BEAST, Race.DEMON, Race.DRAGON, Race.ELEMENTAL, Race.MECHANICAL, Race.MURLOC, Race.PIRATE,
            Race.TOTEM, Race.UNDEAD, Race.DRAENEI, Race.QUILBOAR, Race.NAGA, Race.ALL };
        var pool0 = Cards.RandomPool(game, PoolDestination.Hand);
        foreach (var card in pool0.All)
        {
            var m = byId[card.Id];
            Check(card.Cost == m.Cost, $"{card.Id} Card.Cost {card.Cost} == member {m.Cost}");
            Check(card.Type.ToString() == m.Type, $"{card.Id} type");
            Check(card.Rarity.ToString() == m.Rarity, $"{card.Id} rarity");
            if (card.Type == CardType.MINION)
            {
                Check(card.ATK == m.Attack && card.Health == m.Health, $"{card.Id} stats");
                foreach (var race in races)
                {
                    bool expected = race == Race.ALL ? m.Races.Contains("ALL") : m.Races.Contains("ALL") || m.Races.Contains(race.ToString());
                    Check(card.IsRace(race) == expected, $"{card.Id} race {race}: card {card.IsRace(race)} member {expected}");
                }
            }
        }
        foreach (var cls in Classes)
        {
            string name = cls == CardClass.DEMONHUNTER ? "DEMONHUNTER" : cls.ToString();
            var classAndNeutral = pool0.ClassAndNeutral(cls);
            var classOnly = pool0.ClassOnly(cls);
            Check(classOnly.Count > 10, cls + ": class cards exist");
            Check(classOnly.All(c => byId[c.Id].Classes.Contains(name)), cls + ": class-only lists the class");
            Check(classAndNeutral.All(c => byId[c.Id].Classes.Contains(name) || byId[c.Id].Classes.Contains("NEUTRAL")), cls + ": class+neutral");
            Check(classAndNeutral.Count == pool0.All.Count(c => byId[c.Id].Classes.Contains(name) || byId[c.Id].Classes.Contains("NEUTRAL")), cls + ": class+neutral complete");
            Check(pool0.Secrets(cls).All(c => c.IsSecret) && pool0.Secrets(cls).Count == classAndNeutral.Count(c => c.IsSecret), cls + ": secrets");
            Console.WriteLine($"  {cls}: class {classOnly.Count}, class+neutral {classAndNeutral.Count}, secrets {pool0.Secrets(cls).Count}");
        }
        // A multi-class card counts for every class it lists.
        var multi = manifest.Standard.Where(m => m.Type != "HERO" && m.Classes.Count(c => c != "NEUTRAL") > 1).ToArray();
        foreach (var m in multi.Where(m => pool0.All.Any(c => c.Id == m.CardId)))
            foreach (var c in m.Classes)
            {
                var cls = (CardClass)Enum.Parse(typeof(CardClass), c);
                Check(pool0.ClassOnly(cls).Any(x => x.Id == m.CardId), $"multi-class {m.CardId} counts for {c}");
            }
        Console.WriteLine($"  members: {pool0.All.Count} cards, {multi.Length} multi-class members");
    }

    private static void CostBuckets()
    {
        var game = NewGame();
        var pool = Cards.RandomPool(game, PoolDestination.Summon);
        var minions = pool.All.Where(c => c.Type == CardType.MINION).ToArray();
        int total = 0;
        for (int cost = -1; cost <= 30; cost++)
        {
            var bucket = pool.MinionsOfCost(cost);
            Check(bucket.All(c => c.Type == CardType.MINION && c.Cost == cost), $"cost {cost} bucket holds only cost-{cost} minions");
            total += bucket.Count;
        }
        Check(total == minions.Length, "the buckets partition the minion pool");
        Check(pool.TryMinionsOfCost(3, out var three) && three.Count > 5, "cost 3 bucket exists");
        // Frozen buckets predate cost changes; the new buckets must follow current values.
        var frozen = Cards.CostMinionCards(FormatType.FT_STANDARD);
        int moved = frozen.Sum(p => p.Value.Count(c => c.Cost != p.Key));
        Console.WriteLine($"  frozen Standard buckets holding a card whose current cost differs: {moved}");
        Check(moved > 0, "premise: the frozen buckets are stale for some cards (otherwise this proof is vacuous)");
        // Bucket cache is per manifest and destination, not per source.
        Check(ReferenceEquals(pool, Cards.RandomPool(game, PoolDestination.Summon)), "same pool instance within a manifest");
        Check(!ReferenceEquals(pool, Cards.RandomPool(game, PoolDestination.Hand)), "destinations are separate pools");
    }

    private static void WildHook()
    {
        var standard = Cards.RandomPool(NewGame(), PoolDestination.Hand);
        var wild = Cards.RandomPool(NewGame(format: GenerationFormat.Wild), PoolDestination.Hand);
        Check(!wild.Legacy, "Wild policy is not legacy");
        Check(wild.Format == GenerationFormat.Wild && standard.Format == GenerationFormat.Standard, "formats are keyed");
        Check(standard.All.Select(c => c.Id).SequenceEqual(wild.All.Select(c => c.Id)), "Wild equals Standard until a Wild list exists");
        var standardIds = standard.All.Select(c => c.Id).ToHashSet();
        var legacyOnly = Cards.AllWild.Where(c => !standardIds.Contains(c.Id)).Select(c => c.Id).ToHashSet();
        Check(legacyOnly.Count > 500, "premise: AllWild holds many non-Standard cards");
        Check(!wild.All.Any(c => legacyOnly.Contains(c.Id)), "Wild policy never reads AllWild");
        // FormatType is not the key: FT_STANDARD and FT_WILD games with the same policy share one pool.
        Check(ReferenceEquals(standard, Cards.RandomPool(NewGame(formatType: FormatType.FT_STANDARD), PoolDestination.Hand)),
            "FormatType does not select the pool");
        Check(ReferenceEquals(standard, Cards.RandomPool(NewGame(formatType: FormatType.FT_WILD), PoolDestination.Hand)),
            "FT_WILD with Standard policy shares the Standard pool");
    }

    private static void LegacyIdentity()
    {
        foreach (var (legacy, unversioned) in new[] { (true, false), (false, true) })
        foreach (var formatType in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            var game = NewGame(legacy: legacy, unversioned: unversioned, formatType: formatType);
            var pool = Cards.RandomPool(game);
            string label = $"legacy={legacy} unversioned={unversioned} {formatType}";
            Check(pool.Legacy, label + ": legacy mode");
            var all = formatType == FormatType.FT_STANDARD ? Cards.AllStandard : Cards.AllWild;
            Check(ReferenceEquals(pool.All, all), label + ": same All list object");
            foreach (var cls in Classes)
                Check(ReferenceEquals(pool.ClassAndNeutral(cls), Cards.FormatTypeClassCards(formatType)[cls]), label + ": same class list " + cls);
            var buckets = Cards.CostMinionCards(formatType);
            foreach (var cost in buckets.Keys)
            {
                Check(ReferenceEquals(pool.MinionsOfCost(cost), buckets[cost]), label + ": same cost list " + cost);
                Check(pool.TryMinionsOfCost(cost, out var b) && ReferenceEquals(b, buckets[cost]), label + ": same TryGet list");
            }
            var sample = all.ToArray();
            Check(ReferenceEquals(pool.WithoutSource(sample, game.Player1.Hero), sample), label + ": legacy keeps source rolls");
        }
        // The legacy flag survives clone.
        var g = NewGame(unversioned: true);
        Check(g.CloneExact().UnversionedRandomPoolReplay && Cards.RandomPool(g.CloneExact()).Legacy, "clone keeps the unversioned flag");
    }

    private static void Isolation()
    {
        // Different pinned manifests in one process: lists never cross, and each pool is cached per manifest instance.
        var installed = NewGame();
        var pinned = PinnedPoolFixture.Use(NewGame());
        Check(!ReferenceEquals(installed.OfflinePools, pinned.OfflinePools), "premise: two manifests");
        var a = Cards.RandomPool(installed, PoolDestination.Hand);
        var b = Cards.RandomPool(pinned, PoolDestination.Hand);
        Check(!ReferenceEquals(a, b), "pools are not shared across manifests");
        Check(b.ManifestHash == pinned.OfflinePools.Hash && a.ManifestHash == installed.OfflinePools.Hash, "pool remembers its manifest");
        var pinnedIds = pinned.OfflinePools.Standard.Select(m => m.CardId).ToHashSet();
        var installedIds = installed.OfflinePools.Standard.Select(m => m.CardId).ToHashSet();
        Check(b.All.All(c => pinnedIds.Contains(c.Id)), "pinned pool only holds pinned members");
        Check(a.All.All(c => installedIds.Contains(c.Id)), "installed pool only holds installed members");
        // Interleave: the later lookup still answers from its own manifest.
        for (int i = 0; i < 4; i++)
        {
            Check(ReferenceEquals(Cards.RandomPool(installed, PoolDestination.Hand), a), "installed stays installed " + i);
            Check(ReferenceEquals(Cards.RandomPool(pinned, PoolDestination.Hand), b), "pinned stays pinned " + i);
        }
        foreach (var cls in new[] { CardClass.MAGE, CardClass.WARRIOR })
        {
            var ca = a.ClassAndNeutral(cls); var cb = b.ClassAndNeutral(cls);
            Check(ca.All(c => installedIds.Contains(c.Id)) && cb.All(c => pinnedIds.Contains(c.Id)), "class lists stay in their manifest " + cls);
        }
    }

    // The sim records the policy version in every new record; a record without it (schema 3 written before #141) and a
    // schema-2 record replay on the frozen lists. Same gate as Dark Gifts.
    private static void ReplayGate()
    {
        HsSim.GameRecord Record(string version, int schema) => new HsSim.GameRecord {
            ReplaySchema = schema, Suite = "core-2026", Player = "core-2026-1", Opponent = "core-2026-1", Seed = 3, Policy = "random",
            PoolPolicyVersion = schema == 3 ? PinnedPoolFixture.Manifest.PolicyVersion : null,
            PoolManifestHash = schema == 3 ? PinnedPoolFixture.Hash : null, RandomPoolPolicyVersion = version };
        var versioned = HsSim.Simulation.Create(Record(MatchRandomPool.PolicyVersion, 3), replay: true, pools: PinnedPoolFixture.Manifest);
        Check(!Cards.RandomPool(versioned).Legacy && Cards.RandomPool(versioned).ManifestHash == PinnedPoolFixture.Hash, "a versioned schema-3 record replays on its pinned manifest");
        var unversioned = HsSim.Simulation.Create(Record(null, 3), replay: true, pools: PinnedPoolFixture.Manifest);
        Check(Cards.RandomPool(unversioned).Legacy, "an unversioned schema-3 record replays on the frozen lists");
        var schema2 = HsSim.Simulation.Create(Record(null, 2), replay: true);
        Check(Cards.RandomPool(schema2).Legacy, "a schema-2 record replays on the frozen lists");
        var fresh = Record(null, 3); fresh.ReplaySchema = null; fresh.PoolPolicyVersion = null; fresh.PoolManifestHash = null;
        var live = HsSim.Simulation.Create(fresh, replay: false);
        Check(fresh.RandomPoolPolicyVersion == MatchRandomPool.PolicyVersion && !Cards.RandomPool(live).Legacy && live.PoolFormat == GenerationFormat.Standard && live.FormatType == FormatType.FT_WILD,
            "a new record pins the policy, plays the Standard pool and keeps FT_WILD");
        bool refused = false;
        try { HsSim.Simulation.Create(Record("match-random/99", 3), replay: true, pools: PinnedPoolFixture.Manifest); }
        catch (HsSim.InvariantException) { refused = true; }
        Check(refused, "a record naming another policy version is refused");
    }

    private static void PolicySurvives()
    {
        var game = NewGame(format: GenerationFormat.Wild);
        Check(game.PoolFormat == GenerationFormat.Wild, "policy read from config");
        Check(game.Clone().PoolFormat == GenerationFormat.Wild && game.CloneExact().PoolFormat == GenerationFormat.Wild, "clone keeps policy");
        var bytes = game.SaveSnapshot();
        var loaded = Game.LoadSnapshot(bytes);
        Check(loaded.PoolFormat == GenerationFormat.Wild, "snapshot round trip keeps policy");
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "snapshot bytes are stable");
        Check(Cards.RandomPool(loaded).Format == GenerationFormat.Wild, "pool follows the loaded game's policy");
        var mismatch = false;
        try { Game.LoadSnapshot(bytes, new GameConfig { FormatType = FormatType.FT_WILD, History = true, Logging = false }); }
        catch (System.Runtime.Serialization.SerializationException) { mismatch = true; }
        Check(mismatch, "a config with another policy is rejected on load");
        var unversioned = NewGame(unversioned: true);
        Check(Game.LoadSnapshot(unversioned.SaveSnapshot()).UnversionedRandomPoolReplay, "snapshot keeps the unversioned replay flag");
        // The default policy is Standard; FT_WILD (what the live match sets) does not change it.
        Check(new GameConfig().PoolFormat == GenerationFormat.Standard && NewGame().PoolFormat == GenerationFormat.Standard, "default policy is Standard");
    }
}
