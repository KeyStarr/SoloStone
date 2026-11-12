using System.Diagnostics;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

// Legacy generators once cached pools process-wide under keys that left out format and/or class,
// so the first game in a process fixed the pool for every later game. Each case below runs game A
// and then game B in this process (both orders) and requires B's result from B's own pool.
internal static class PoolCacheIsolationProof
{
    private static readonly FormatType[] Formats = { FormatType.FT_STANDARD, FormatType.FT_WILD };
    private static readonly int[] Seeds = { 1, 17, 91, 307, 1009 };
    private static readonly int[] ClassSeeds = Enumerable.Range(1, 40).ToArray();
    private static int _cases;

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("pool cache isolation: " + message);
    }

    private static Game NewGame(FormatType format, CardClass p1, CardClass p2, int seed,
        IEnumerable<Card> p2Deck = null)
    {
        var game = new Game(new GameConfig {
            RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Logging = false, History = true, FormatType = format,
            Player1HeroClass = p1, Player2HeroClass = p2,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList(),
            Player2Deck = (p2Deck ?? Enumerable.Repeat(Cards.FromId("CS2_179"), 30)).ToList()
        });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }

    private static Card[] Offer(Game game, ISimpleTask task, IEntity source = null)
    {
        var player = game.Player1;
        player.Choice = null;
        task.Process(game, player, source ?? player.Hero, null, new TaskStack());
        return player.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
    }

    private static string Ids(IEnumerable<Card> cards) => string.Join(",", cards.Select(c => c.Id));

    // Exact oracle: the same RNG cursor applied to the pool this game must use. Pools must differ
    // between the two settings, so a pool left over from the other game cannot match by accident.
    private static void Exact(string name, Func<FormatType, Card[][]> pool,
        Func<Game, Card[]> run, Func<Card[][], Util.DeepCloneableRandom, Card[]> sample,
        CardClass cls = CardClass.MAGE)
    {
        Check(Ids(pool(FormatType.FT_STANDARD).SelectMany(s => s)) != Ids(pool(FormatType.FT_WILD).SelectMany(s => s)),
            name + ": Standard and Wild pools must differ");
        foreach (var order in new[] { Formats, Formats.Reverse().ToArray() })
        foreach (var format in order)
        foreach (int seed in Seeds)
        {
            var game = NewGame(format, cls, CardClass.WARRIOR, seed);
            var rng = game.Random.Clone();
            string expected = Ids(sample(pool(format), rng));
            string actual = Ids(run(game));
            Check(actual == expected, $"{name} {format} after {order[0]} seed={seed}: {actual} != {expected}");
            _cases++;
        }
    }

    // Class-disjoint oracle: every result must come from this game's class pool, never the other's.
    private static void ByClass(string name, CardClass a, CardClass b, Func<Game, Card[]> run,
        Func<Game, HashSet<Card>> pool, Func<Card, bool> classCard)
    {
        foreach (var order in new[] { new[] { a, b }, new[] { b, a } })
        foreach (var cls in order)
        {
            int classHits = 0;
            foreach (int seed in ClassSeeds)
            {
                var game = NewGame(FormatType.FT_WILD, cls, cls == a ? b : a, seed);
                var offered = run(game);
                var allowed = pool(game);
                Check(offered.Length > 0 && offered.All(allowed.Contains),
                    $"{name} {cls} after {order[0]} seed={seed}: {Ids(offered.Where(c => !allowed.Contains(c)))} outside own pool");
                classHits += offered.Count(classCard);
                _cases++;
            }
            Check(classHits > 0, $"{name} {cls}: no class card offered, so the class pool was not exercised");
        }
    }

    internal static void Run()
    {
        ConcurrentSecrets();
        TypedDiscover();
        CriteriaDiscover();
        RandomCards();
        RandomMinions();
        RandomSpells();
        Glimmerroot();
        BuildABeast();
        Console.WriteLine($"PASS pool cache isolation: {_cases} second-game checks; typed/criteria Discover, RandomCard, " +
            "RandomMinion, CastRandomSpell, Glimmerroot and both Build-A-Beast paths use their own game's format/class pools; mixed-key secrets match serial pools");
    }

    private static Card[][] FilterSets(FormatType format, CardClass cls, Func<Card, bool> filter)
    {
        // GetFilter: [non-class, class-without-quests] from the format's class pool.
        var cards = Cards.FormatTypeClassCards(format)[cls];
        return new[] {
            cards.Where(p => p.Class != cls && filter(p)).ToArray(),
            cards.Where(p => p.Class == cls && !p.IsQuest && filter(p)).ToArray()
        };
    }

    private static void TypedDiscover()
    {
        // Real play: Blazing Invocation (BATTLECRY) and Netherspite Historian (DRAGON) offer class pools.
        foreach (var (type, filter) in new (DiscoverType, Func<Card, bool>)[] {
            (DiscoverType.BATTLECRY, p => p[GameTag.BATTLECRY] == 1 && p.Type == CardType.MINION),
            (DiscoverType.DRAGON, p => p.IsRace(Race.DRAGON)) })
            ByClass("Discover " + type, CardClass.MAGE, CardClass.WARRIOR, g => Offer(g, new DiscoverTask(type)),
                g => FilterSets(g.FormatType, g.Player1.BaseClass, filter).SelectMany(s => s).ToHashSet(),
                c => c.Class != CardClass.NEUTRAL);
        Exact("Discover SPELL", f => FilterSets(f, CardClass.MAGE, p => p.Type == CardType.SPELL),
            g => Offer(g, new DiscoverTask(DiscoverType.SPELL)), (sets, rng) => DiscoverTask.GetChoices(sets, 3, rng));
        Exact("Discover WEAPON_ANOTHERCLASS",
            f => new[] { Cards.FormatTypeCards(f).Where(p => p.Type == CardType.WEAPON && p.Class != CardClass.ROGUE).ToArray() },
            g => Offer(g, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)),
            (sets, rng) => DiscoverTask.GetChoices(new[] { sets[0].Distinct().ToArray() }, 3, rng), CardClass.ROGUE);
    }

    private static void CriteriaDiscover()
    {
        Exact("Discover criteria MINION", f => {
                var cards = Cards.FormatTypeClassCards(f)[CardClass.MAGE].Where(p => p.Type == CardType.MINION).ToArray();
                return new[] { cards.Where(p => p.Class != CardClass.MAGE).ToArray(), cards.Where(p => p.Class == CardClass.MAGE).ToArray() };
            },
            g => Offer(g, new DiscoverTask(CardType.MINION)), (sets, rng) => DiscoverTask.GetChoices(sets, 3, rng));
        // Each order starts with a cold criteria cache, including when this proof follows other tests.
        foreach (int first in new[] { 3, 1 })
        {
            var start = new ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--pool-criteria-child");
            start.ArgumentList.Add(first.ToString());
            using var child = Process.Start(start);
            var output = child.StandardOutput.ReadToEndAsync();
            var error = child.StandardError.ReadToEndAsync();
            if (!child.WaitForExit(30000))
            {
                child.Kill();
                child.WaitForExit();
                Check(false, $"criteria order starting cost<={first} timed out");
            }
            Check(child.ExitCode == 0, $"cold criteria order starting cost<={first}: {output.Result}{error.Result}");
            Console.Write(output.Result);
            _cases += 2 * Seeds.Length;
        }
    }

    internal static void CriteriaChild(int first)
    {
        Check(first == 1 || first == 3, "unknown criteria order");
        var order = first == 3 ? new[] { 3, 1 } : new[] { 1, 3 };
        // A repeated Discover retains the complete eligible sets for its next offer.
        var cardSets = typeof(Choice).GetField("_cardSets", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (int max in order)
        foreach (int seed in Seeds)
        {
            var game = NewGame(FormatType.FT_WILD, CardClass.MAGE, CardClass.WARRIOR, seed);
            var pool = Cards.FormatTypeClassCards(game.FormatType)[CardClass.MAGE]
                .Where(c => c.Type == CardType.SPELL && c.Cost <= max).ToArray();
            var sets = new[] { pool.Where(c => c.Class != CardClass.MAGE).ToArray(), pool.Where(c => c.Class == CardClass.MAGE).ToArray() };
            string expected = Ids(DiscoverTask.GetChoices(sets, 3, game.Random.Clone()));
            var offered = Offer(game, new DiscoverTask(CardType.SPELL, tagValueCriteria: (GameTag.COST, RelaSign.LEQ, max), repeat: 2));
            var actualSets = (Card[][])cardSets.GetValue(game.Player1.Choice.NextChoice);
            Check(actualSets.Length == sets.Length && actualSets.Zip(sets).All(pair => pair.First.SequenceEqual(pair.Second)),
                $"Discover criteria SPELL cost<={max} after cost<={first} seed={seed}: complete eligible pools differ");
            Check(Ids(offered) == expected,
                $"Discover criteria SPELL cost<={max} after cost<={first} seed={seed}: {Ids(offered)} != {expected}");
        }
        Console.WriteLine($"PASS cold criteria order {string.Join(",", order)}: complete eligible pools and exact seeded offers match independent filters");
    }

    private static void ConcurrentSecrets()
    {
        // Exercise the shared helper used by both Arcane Keysmith and CastRandomSecret, without casting effects.
        var getSecrets = typeof(SpecificTask).GetMethod("GetFormatTypeClassSecrets", BindingFlags.Static | BindingFlags.NonPublic)
            .CreateDelegate<Func<FormatType, CardClass, Card[]>>();
        var keys = Formats.SelectMany(f => new[] { CardClass.MAGE, CardClass.HUNTER, CardClass.PALADIN, CardClass.ROGUE }
            .Select(c => (Format: f, Class: c))).ToArray();
        // Initialize card data without warming the secret cache; concurrent first calls must insert/resize it.
        var expected = keys.Select(k => Cards.FormatTypeClassCards(k.Format)[k.Class].Where(c => c.IsSecret).ToArray()).ToArray();
        const int attempts = 1000;
        var results = new Card[keys.Length][][];
        using var start = new Barrier(keys.Length);
        var workers = keys.Select((key, i) => Task.Factory.StartNew(() => {
            results[i] = new Card[attempts][];
            Check(start.SignalAndWait(TimeSpan.FromSeconds(30)), "secret worker start timed out");
            for (int n = 0; n < attempts; n++)
                results[i][n] = getSecrets(key.Format, key.Class);
        }, TaskCreationOptions.LongRunning)).ToArray();
        Task.WaitAll(workers);
        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var serial = getSecrets(key.Format, key.Class);
            Check(serial.SequenceEqual(expected[i]), $"secret {key}: serial pool differs from independent filter/order");
            foreach (var result in results[i])
            {
                Check(result.SequenceEqual(serial), $"secret {key}: concurrent pool differs from serial pool/order");
                _cases++;
            }
        }
    }

    private static Card[] Generated(Game game, ISimpleTask task, IEntity source)
    {
        var stack = new TaskStack();
        task.Process(game, game.Player1, source, null, stack);
        return stack.Playables.Select(p => p.Card).ToArray();
    }

    private static void RandomCards()
    {
        Card[] Pool(FormatType f, CardType type) => (f == FormatType.FT_STANDARD ? Cards.AllStandard : Cards.AllWild)
            .Where(p => p.Type == type && p.Class == CardClass.MAGE && p[GameTag.QUEST] == 0).ToArray();
        // The old key was (source, class): one source with two filters reused the first filter's pool.
        var source = Cards.FromId("CS2_179");
        Exact("RandomCard SPELL then MINION (same source)",
            f => new[] { Pool(f, CardType.SPELL), Pool(f, CardType.MINION) },
            g => {
                var s = Entity.FromCard(g.Player1, source);
                return Generated(g, new RandomCardTask(CardType.SPELL, CardClass.MAGE), s)
                    .Concat(Generated(g, new RandomCardTask(CardType.MINION, CardClass.MAGE), s)).ToArray();
            },
            (sets, rng) => new[] { sets[0].Choose(rng), sets[1].Choose(rng) });
    }

    private static void RandomMinions()
    {
        // Malchezaar-style class pool (controller.Standard/Wild) from one source in both classes.
        ByClass("RandomMinion class legendary", CardClass.MAGE, CardClass.WARRIOR,
            g => Generated(g, new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY, classAndMultiOnlyFlag: true),
                Entity.FromCard(g.Player1, Cards.FromId("KAR_096"))),
            g => g.Player1.Wild.Where(c => c.Type == CardType.MINION && c.Rarity == Rarity.LEGENDARY).ToHashSet(),
            c => c.Class != CardClass.NEUTRAL);
        // Jeweled Macaw-style race pool from one source in both formats.
        Exact("RandomMinion BEAST",
            f => new[] { (f == FormatType.FT_STANDARD ? Cards.AllStandard : Cards.AllWild)
                .Where(p => p.Type == CardType.MINION && p.IsRaceForLegacyRandomPool(Race.BEAST)).ToArray() },
            g => Generated(g, new RandomMinionTask(GameTag.CARDRACE, (int)Race.BEAST),
                Entity.FromCard(g.Player1, Cards.FromId("CORE_UNG_912"))),
            (sets, rng) => new[] { sets[0].Choose(rng) });
    }

    private static void RandomSpells()
    {
        Func<Card, bool> oneCost = c => c.Cost == 1;
        // Two casts per game: the old cache hit for a conditional source fell through to the unfiltered pool.
        Exact("CastRandomSpell cost 1 (twice)",
            f => new[] { Cards.FormatTypeCards(f).Where(c => c.Type == CardType.SPELL && oneCost(c)).ToArray() },
            g => {
                var source = Entity.FromCard(g.Player1, Cards.FromId("OG_087"));
                var cast = new List<Card>();
                for (int i = 0; i < 2; i++)
                {
                    var rng = g.Random.Clone();
                    var before = g.IdEntityDic.Values.Select(e => e.Id).ToHashSet();
                    new CastRandomSpellTask(oneCost).Process(g, g.Player1, source, null, new TaskStack());
                    cast.Add(g.IdEntityDic.Values.Where(e => !before.Contains(e.Id)).OrderBy(e => e.Id).First().Card);
                    // Only the pick is compared; reseed past the cast spell's own random effects.
                    g.SetRandomSeed(rng.Next());
                }
                return cast.ToArray();
            },
            (sets, rng) => {
                var cast = new List<Card>();
                for (int i = 0; i < 2; i++)
                {
                    var cursor = rng.Clone();
                    Card pick;
                    do pick = sets[0][cursor.Next(sets[0].Length)]; while (!pick.Implemented || pick.HideStat);
                    cast.Add(pick);
                    rng = new Util.DeepCloneableRandom(rng.Next());
                }
                return cast.ToArray();
            });
    }

    private static void Glimmerroot()
    {
        // Curious Glimmerroot: one card from the opponent's starting class cards plus two class decoys.
        // Two Mage decks as well as a Paladin deck: the memory must follow the deck, not only the class.
        // A leaked deck offers its own card plus decoys, so this deck's cards show up 0 to 2 times, not exactly once.
        var decks = new (string Name, CardClass Class, Card[] Cards)[] {
            ("mage-a", CardClass.MAGE, new[] { "CS2_029", "CS2_022", "EX1_277" }.Select(Cards.FromId).ToArray()),
            ("mage-b", CardClass.MAGE, new[] { "CS2_024", "CS2_026", "CS2_032" }.Select(Cards.FromId).ToArray()),
            ("paladin", CardClass.PALADIN, new[] { "CS2_087", "CS2_093", "EX1_360" }.Select(Cards.FromId).ToArray())
        };
        foreach (var order in new[] { decks, decks.Reverse().ToArray() })
        foreach (var opDeck in order)
        foreach (int seed in Seeds)
        {
            var deck = Enumerable.Range(0, 30).Select(i => opDeck.Cards[i % 3]).ToArray();
            var game = NewGame(FormatType.FT_WILD, CardClass.PRIEST, opDeck.Class, seed, deck);
            var offered = Offer(game, SpecificTask.CuriousGlimmerroot);
            var classPool = Cards.FormatTypeClassCards(game.FormatType)[opDeck.Class].Where(c => c.Class == opDeck.Class).ToHashSet();
            Check(offered.Length == 3 && offered.Count(opDeck.Cards.Contains) == 1
                && offered.All(c => opDeck.Cards.Contains(c) || classPool.Contains(c)),
                $"Glimmerroot vs {opDeck.Name} after {order[0].Name} seed={seed}: {Ids(offered)}");
            _cases++;
        }
    }

    private static void BuildABeast()
    {
        Card[][] Pools(FormatType f)
        {
            string prefix = LegacyGenerationV1.Format(f);
            return new[] { LegacyGenerationV1.Get(prefix + "/Beasts/First").ToArray(), LegacyGenerationV1.Get(prefix + "/Beasts/Second").ToArray() };
        }
        Card[] Offers(Game game, ISimpleTask task)
        {
            var player = game.Player1;
            player.Choice = null;
            task.Process(game, player, Entity.FromCard(player, Cards.FromId("ICC_828p")), null, new TaskStack());
            return player.Choice.Choices.Concat(player.Choice.NextChoice.Choices).Select(id => game.IdEntityDic[id].Card).ToArray();
        }
        Card[] Sample(Card[][] sets, Util.DeepCloneableRandom rng) => sets[0].ChooseNElements(3, rng).Concat(sets[1].ChooseNElements(3, rng)).ToArray();
        Exact("Build-A-Beast legacy", Pools, g => Offers(g, SpecificTask.BuildABeast), Sample, CardClass.HUNTER);
        Exact("Build-A-Beast module", Pools, g => Offers(g, Cards.FromId("ICC_828p").Power.PowerTask), Sample, CardClass.HUNTER);
    }
}
