using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.SimpleTasks;

// #141 sampler: runs every migrated random task shape in games configured like the live match
// (FormatType FT_WILD, pool policy Standard) and in FT_STANDARD games, over many seeds and hero classes, and requires
// that every generated card is in the active Standard pool and matches the task's own filter.
// Cost-N picks have cost N (current value), race picks have the race (IsRace), class picks list the class.
internal static class MatchRandomSamplerProof
{
    // Task shapes covered by the sampler, by migration step.
    internal static readonly string[] DefaultGroups = { "minion", "card", "spell", "discover", "secret", "deck", "format" };

    private sealed class Row
    {
        public string Name, Group;
        public Func<Game, Controller, IEnumerable<Card>> Run;
        public Func<Game, Controller, Card, bool> Ok;
        public bool Fresh; // needs its own game (casts, secrets, hero power swaps)
    }

    private static readonly CardClass[] HeroClasses = { CardClass.MAGE, CardClass.WARRIOR, CardClass.DRUID, CardClass.HUNTER,
        CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT };

    private static string ClassName(CardClass c) => (int)c == 14 ? "DEMONHUNTER" : c.ToString();

    private static Game NewGame(int seed, FormatType format, CardClass hero, CardClass opponent = CardClass.WARRIOR,
        IEnumerable<Card> opponentDeck = null, IEnumerable<Card> deck = null, bool fill = false, bool unversioned = false)
    {
        var game = new Game(new GameConfig {
            RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true, Logging = false, History = true,
            FormatType = format, PoolFormat = GenerationFormat.Standard, UnversionedRandomPoolReplay = unversioned, EnableCorpses = hero == CardClass.DEATHKNIGHT || opponent == CardClass.DEATHKNIGHT,
            Player1HeroClass = hero, Player2HeroClass = opponent, FillDecks = fill,
            Player1Deck = (deck ?? Enumerable.Repeat(Cards.FromId("CS2_179"), 30)).ToList(),
            Player2Deck = (opponentDeck ?? Enumerable.Repeat(Cards.FromId("CS2_179"), 30)).ToList() });
        game.StartGame();
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        return game;
    }

    private static IEnumerable<Card> Gen(Game g, Controller c, ISimpleTask task, IEntity source = null, IPlayable target = null)
    {
        var stack = new TaskStack();
        task.Process(g, c, source ?? c.Hero, target, stack);
        return stack.Playables?.Select(p => p.Card).ToArray() ?? Array.Empty<Card>();
    }

    private static IEnumerable<Card> Offer(Game g, Controller c, ISimpleTask task, IEntity source = null)
    {
        c.Choice = null;
        task.Process(g, c, source ?? c.Hero, null, new TaskStack());
        return c.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
    }

    // The first entity a task creates is the card it picked (casts), ignoring the spell's own later effects.
    private static IEnumerable<Card> Cast(Game g, Controller c, ISimpleTask task, IEntity source)
    {
        var before = g.IdEntityDic.Values.Select(e => e.Id).ToHashSet();
        try { task.Process(g, c, source, null, new TaskStack()); } catch (Exception) { /* the pick is what matters */ }
        var made = g.IdEntityDic.Values.Where(e => !before.Contains(e.Id)).OrderBy(e => e.Id).FirstOrDefault();
        return made == null ? Array.Empty<Card>() : new[] { made.Card };
    }

    private static List<Row> Rows(Dictionary<string, OfflinePoolPolicy.Member> members)
    {
        bool Cls(Card c, CardClass cls) => members.TryGetValue(c.Id, out var m) && m.Classes.Contains(ClassName(cls));
        bool ClsOrNeutral(Card c, CardClass cls) => Cls(c, cls) || members.TryGetValue(c.Id, out var m) && m.Classes.Contains("NEUTRAL");
        var rows = new List<Row>();
        Row Add(string group, string name, Func<Game, Controller, IEnumerable<Card>> run, Func<Game, Controller, Card, bool> ok, bool fresh = false)
        { var r = new Row { Group = group, Name = name, Run = run, Ok = ok, Fresh = fresh }; rows.Add(r); return r; }

        // RandomMinionTask / SummonRandomMinion shapes
        Add("minion", "RandomMinionTask RARITY=LEGENDARY", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY)),
            (g, c, x) => x.Type == CardType.MINION && x.Rarity == Rarity.LEGENDARY);
        Add("minion", "RandomMinionTask RARITY=LEGENDARY class+neutral (Malchezaar)", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY, 1, RelaSign.EQ, true, true)),
            (g, c, x) => x.Type == CardType.MINION && x.Rarity == Rarity.LEGENDARY && ClsOrNeutral(x, c.HeroClass));
        for (int n = 0; n <= 10; n++)
        {
            int cost = n;
            Add("minion", $"RandomMinionTask COST={cost}", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.COST, cost)),
                (g, c, x) => x.Type == CardType.MINION && x.Cost == cost);
        }
        Add("minion", "RandomMinionTask COST>=5", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.COST, 5, 1, RelaSign.GEQ)),
            (g, c, x) => x.Type == CardType.MINION && x.Cost >= 5);
        Add("minion", "RandomMinionTask COST<=2", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.COST, 2, 1, RelaSign.LEQ)),
            (g, c, x) => x.Type == CardType.MINION && x.Cost <= 2);
        Add("minion", "RandomMinionTask COST=3 amount 3", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.COST, 3, 3)),
            (g, c, x) => x.Type == CardType.MINION && x.Cost == 3);
        Add("minion", "RandomMinionTask COST=target cost", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.COST, EntityType.TARGET),
                target: Entity.FromCard(c, Cards.FromId("CS2_201"))),
            (g, c, x) => x.Type == CardType.MINION && x.Cost == Cards.FromId("CS2_201").Cost);
        foreach (var race in new[] { Race.BEAST, Race.DEMON, Race.MURLOC, Race.DRAGON, Race.MECHANICAL, Race.ELEMENTAL, Race.PIRATE })
        {
            var r = race;
            Add("minion", $"RandomMinionTask CARDRACE={r}", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.CARDRACE, (int)r)),
                (g, c, x) => x.Type == CardType.MINION && x.IsRace(r));
        }
        Add("minion", "RandomMinionTask ATK>=5", (g, c) => Gen(g, c, new RandomMinionTask(GameTag.ATK, 5, 1, RelaSign.GEQ)),
            (g, c, x) => x.Type == CardType.MINION && x.ATK >= 5);
        Add("minion", "SummonRandomMinion(COST,4) task list", (g, c) => Gen(g, c, ComplexTask.Create(new RandomMinionTask(GameTag.COST, 4))),
            (g, c, x) => x.Type == CardType.MINION && x.Cost == 4);
        for (int n = 0; n <= 10; n++)
        {
            int cost = n;
            Add("minion", $"RandomMinionNumberTask COST=stack number {cost}", (g, c) => {
                    var stack = new TaskStack { Number = cost };
                    new RandomMinionNumberTask(GameTag.COST).Process(g, c, c.Hero, null, stack);
                    return stack.Playables?.Select(p => p.Card).ToArray() ?? Array.Empty<Card>();
                },
                (g, c, x) => x.Type == CardType.MINION && x.Cost == cost);
        }
        for (int n = 1; n <= 6; n++)
        {
            int attack = n;
            Add("minion", $"RandomMinionNumberTask ATK={attack}", (g, c) => {
                    var stack = new TaskStack { Number = attack };
                    new RandomMinionNumberTask(GameTag.ATK).Process(g, c, c.Hero, null, stack);
                    return stack.Playables?.Select(p => p.Card).ToArray() ?? Array.Empty<Card>();
                },
                (g, c, x) => x.Type == CardType.MINION && x.ATK == attack);
        }
        foreach (var (from, k) in new[] { ("CS2_201", -2), ("CS2_201", -1), ("CS2_201", 0), ("CS2_201", 1), ("CS2_201", 2), ("CS2_189", 1), ("CS2_122", 3) })
        {
            string id = from; int change = k;
            Add("minion", $"TransformMinionTask {id} {change:+0;-0;0}", (g, c) => {
                    var minion = (Minion)Entity.FromCard(c, Cards.FromId(id), null, c.BoardZone);
                    var stack = new TaskStack { Playables = new List<IPlayable> { minion } };
                    new TransformMinionTask(EntityType.STACK, change).Process(g, c, c.Hero, null, stack);
                    var result = c.BoardZone.Select(m => m.Card).ToArray();
                    foreach (var m in c.BoardZone.ToArray()) c.BoardZone.Remove(m);
                    return result;
                },
                (g, c, x) => x.Type == CardType.MINION && x.Cost == Cards.FromId(id).Cost + change);
        }

        // RandomCardTask shapes
        Add("card", "RandomCardTask HERO class", (g, c) => Gen(g, c, new RandomCardTask(EntityType.HERO)),
            (g, c, x) => Cls(x, c.HeroClass) && x[GameTag.QUEST] == 0);
        Add("card", "RandomCardTask OP_HERO class", (g, c) => Gen(g, c, new RandomCardTask(EntityType.OP_HERO)),
            (g, c, x) => Cls(x, c.Opponent.HeroClass) && x[GameTag.QUEST] == 0);
        foreach (var cls in new[] { CardClass.MAGE, CardClass.HUNTER, CardClass.DRUID, CardClass.PRIEST, CardClass.PALADIN, CardClass.DEMONHUNTER })
        {
            var cc = cls;
            Add("card", $"RandomCardTask SPELL {cc}", (g, c) => Gen(g, c, new RandomCardTask(CardType.SPELL, cc)),
                (g, c, x) => x.Type == CardType.SPELL && Cls(x, cc) && x[GameTag.QUEST] == 0);
        }
        Add("card", "RandomCardTask MINION DEATHKNIGHT", (g, c) => Gen(g, c, new RandomCardTask(CardType.MINION, CardClass.DEATHKNIGHT)),
            (g, c, x) => x.Type == CardType.MINION && Cls(x, CardClass.DEATHKNIGHT));
        Add("card", "RandomCardTask PALADIN any type", (g, c) => Gen(g, c, new RandomCardTask(CardType.INVALID, CardClass.PALADIN)),
            (g, c, x) => Cls(x, CardClass.PALADIN));
        foreach (var race in new[] { Race.BEAST, Race.DEMON, Race.DRAGON, Race.ELEMENTAL, Race.MECHANICAL, Race.MURLOC })
        {
            var r = race;
            Add("card", $"RandomCardTask MINION race {r}", (g, c) => Gen(g, c, new RandomCardTask(CardType.MINION, CardClass.INVALID, r)),
                (g, c, x) => x.Type == CardType.MINION && x.IsRace(r));
            Add("card", $"RandomCardTask any type race {r}", (g, c) => Gen(g, c, new RandomCardTask(CardType.INVALID, CardClass.INVALID, r)),
                (g, c, x) => x.IsRace(r));
        }
        Add("card", "RandomCardTask LEGENDARY any", (g, c) => Gen(g, c, new RandomCardTask(CardType.INVALID, CardClass.INVALID, Race.INVALID, Rarity.LEGENDARY)),
            (g, c, x) => x.Rarity == Rarity.LEGENDARY);
        Add("card", "RandomCardTask MINION LEGENDARY", (g, c) => Gen(g, c, new RandomCardTask(CardType.MINION, CardClass.INVALID, Race.INVALID, Rarity.LEGENDARY)),
            (g, c, x) => x.Type == CardType.MINION && x.Rarity == Rarity.LEGENDARY);
        Add("card", "RandomCardTask DEATHRATTLE tag", (g, c) => Gen(g, c, new RandomCardTask(CardType.MINION, CardClass.INVALID, Race.INVALID, Rarity.INVALID, new[] { GameTag.DEATHRATTLE })),
            (g, c, x) => x.Type == CardType.MINION && x.Tags.ContainsKey(GameTag.DEATHRATTLE));
        foreach (var type in new[] { CardType.MINION, CardType.SPELL, CardType.WEAPON })
        {
            var t = type;
            Add("card", $"RandomCardTask any {t}", (g, c) => Gen(g, c, new RandomCardTask(t, CardClass.INVALID)),
                (g, c, x) => x.Type == t && x[GameTag.QUEST] == 0);
        }
        Add("card", "RandomCardTask opposite spell", (g, c) => Gen(g, c, new RandomCardTask(CardType.SPELL, CardClass.INVALID, opposite: true)),
            (g, c, x) => x.Type == CardType.SPELL);
        Add("card", "AddRandomMageSpellToHand", (g, c) => Gen(g, c, new RandomCardTask(CardType.SPELL, CardClass.MAGE)),
            (g, c, x) => x.Type == CardType.SPELL && Cls(x, CardClass.MAGE));

        // CastRandomSpellTask (fresh game: it really casts)
        Add("spell", "CastRandomSpellTask", (g, c) => Cast(g, c, new CastRandomSpellTask(), Entity.FromCard(c, Cards.FromId("OG_134"))),
            (g, c, x) => x.Type == CardType.SPELL && x[GameTag.QUEST] == 0, fresh: true);
        Add("spell", "CastRandomSpellTask cost<6", (g, c) => Cast(g, c, new CastRandomSpellTask(card => card.Cost < 6), Entity.FromCard(c, Cards.FromId("OG_087"))),
            (g, c, x) => x.Type == CardType.SPELL && x.Cost < 6, fresh: true);

        // Discover
        foreach (var type in new[] { DiscoverType.SPELL, DiscoverType.MECHANICAL, DiscoverType.BATTLECRY, DiscoverType.DEATHRATTLE, DiscoverType.MINION,
            DiscoverType.SIX_COST_SUMMON, DiscoverType.SPELL_RANDOM, DiscoverType.TAUNT, DiscoverType.ALL, DiscoverType.OVERLOAD, DiscoverType.DRAGON,
            DiscoverType.BEAST, DiscoverType.MURLOC, DiscoverType.ELEMENTAL, DiscoverType.DEMON, DiscoverType.LEGENDARY_MINIONS,
            DiscoverType.COST_8_MORE_SUMMON, DiscoverType.SPELL_COSTS_5_OR_MORE, DiscoverType.ONE_COST, DiscoverType.THREE_COST,
            DiscoverType.DEATHRATTLE_MINIONS, DiscoverType.OP_HERO, DiscoverType.SECRET })
        {
            var t = type;
            Func<Card, bool> shape = x => true;
            switch (t)
            {
                case DiscoverType.SPELL: shape = x => x.Type == CardType.SPELL; break;
                case DiscoverType.MECHANICAL: shape = x => x.IsRace(Race.MECHANICAL); break;
                case DiscoverType.BATTLECRY: shape = x => x.Type == CardType.MINION && x[GameTag.BATTLECRY] == 1; break;
                case DiscoverType.DEATHRATTLE: shape = x => x[GameTag.DEATHRATTLE] == 1; break;
                case DiscoverType.DEATHRATTLE_MINIONS: shape = x => x.Type == CardType.MINION && x[GameTag.DEATHRATTLE] == 1; break;
                case DiscoverType.MINION: shape = x => x.Type == CardType.MINION; break;
                case DiscoverType.SIX_COST_SUMMON: shape = x => x.Type == CardType.MINION && x.Cost == 6; break;
                case DiscoverType.SPELL_RANDOM: shape = x => x.Type == CardType.SPELL; break;
                case DiscoverType.TAUNT: shape = x => x[GameTag.TAUNT] == 1; break;
                case DiscoverType.DRAGON: shape = x => x.IsRace(Race.DRAGON); break;
                case DiscoverType.BEAST: shape = x => x.IsRace(Race.BEAST); break;
                case DiscoverType.MURLOC: shape = x => x.IsRace(Race.MURLOC); break;
                case DiscoverType.ELEMENTAL: shape = x => x.IsRace(Race.ELEMENTAL); break;
                case DiscoverType.DEMON: shape = x => x.IsRace(Race.DEMON); break;
                case DiscoverType.LEGENDARY_MINIONS: shape = x => x.Type == CardType.MINION && x.Rarity == Rarity.LEGENDARY; break;
                case DiscoverType.COST_8_MORE_SUMMON: shape = x => x.Type == CardType.MINION && x.Cost >= 8; break;
                case DiscoverType.SPELL_COSTS_5_OR_MORE: shape = x => x.Type == CardType.SPELL && x.Cost >= 5; break;
                case DiscoverType.ONE_COST: shape = x => x.Cost == 1; break;
                case DiscoverType.THREE_COST: shape = x => x.Cost == 3; break;
                case DiscoverType.OVERLOAD: shape = x => x.HasOverload; break;
                case DiscoverType.SECRET: shape = x => x[GameTag.SECRET] == 1; break;
            }
            Add("discover", $"Discover {t}", (g, c) => Offer(g, c, new DiscoverTask(t)), (g, c, x) => shape(x));
        }
        Add("discover", "Discover tri-class DRUID_ROGUE_SHAMAN", (g, c) => Offer(g, c, new DiscoverTask(DiscoverType.DRUID_ROGUE_SHAMAN)),
            (g, c, x) => ClsOrNeutral(x, CardClass.DRUID) || ClsOrNeutral(x, CardClass.ROGUE) || ClsOrNeutral(x, CardClass.SHAMAN));
        Add("discover", "Discover tri-class MAGE_PRIEST_WARLOCK", (g, c) => Offer(g, c, new DiscoverTask(DiscoverType.MAGE_PRIEST_WARLOCK)),
            (g, c, x) => ClsOrNeutral(x, CardClass.MAGE) || ClsOrNeutral(x, CardClass.PRIEST) || ClsOrNeutral(x, CardClass.WARLOCK));
        Add("discover", "Discover tri-class HUNTER_PALADIN_WARRIOR", (g, c) => Offer(g, c, new DiscoverTask(DiscoverType.HUNTER_PALADIN_WARRIOR)),
            (g, c, x) => ClsOrNeutral(x, CardClass.HUNTER) || ClsOrNeutral(x, CardClass.PALADIN) || ClsOrNeutral(x, CardClass.WARRIOR));
        Add("discover", "Discover WEAPON_ANOTHERCLASS", (g, c) => Offer(g, c, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)),
            (g, c, x) => x.Type == CardType.WEAPON);
        Add("discover", "Discover criteria MINION", (g, c) => Offer(g, c, new DiscoverTask(CardType.MINION)), (g, c, x) => x.Type == CardType.MINION);
        Add("discover", "Discover criteria SPELL cost<=3", (g, c) => Offer(g, c, new DiscoverTask(CardType.SPELL, tagValueCriteria: (GameTag.COST, RelaSign.LEQ, 3))),
            (g, c, x) => x.Type == CardType.SPELL && x.Cost <= 3);
        Add("discover", "Discover criteria MINION race-free cost=2", (g, c) => Offer(g, c, new DiscoverTask(CardType.MINION, tagValueCriteria: (GameTag.COST, RelaSign.EQ, 2))),
            (g, c, x) => x.Type == CardType.MINION && x.Cost == 2);
        Add("discover", "Discover criteria SPELL ANOTHER_CLASS", (g, c) => Offer(g, c, new DiscoverTask(CardType.SPELL, CardClass.ANOTHER_CLASS)),
            (g, c, x) => x.Type == CardType.SPELL && !ClsOrNeutral(x, c.HeroClass) || x.Type == CardType.SPELL && !Cls(x, c.HeroClass));
        Add("discover", "Discover criteria MINION exact PALADIN", (g, c) => Offer(g, c, new DiscoverTask(CardType.MINION, CardClass.PALADIN)),
            (g, c, x) => x.Type == CardType.MINION && Cls(x, CardClass.PALADIN));

        // Secrets, Glimmerroot, Renounce Darkness (fresh games: they cast or swap hero powers)
        foreach (var cls in new[] { CardClass.MAGE, CardClass.HUNTER, CardClass.PALADIN, CardClass.ROGUE, CardClass.DEMONHUNTER })
        {
            var cc = cls;
            Add("secret", $"CastRandomSecret {cc}", (g, c) => Cast(g, c, SpecificTask.CastRandomSecret(cc), Entity.FromCard(c, Cards.FromId("ICC_204"))),
                (g, c, x) => x.IsSecret && ClsOrNeutral(x, cc), fresh: true);
        }
        Add("secret", "ArcaneKeysmith offers", (g, c) => Offer(g, c, ComplexTask.Create(SpecificTask.ArcaneKeysmith), Entity.FromCard(c, Cards.FromId("GIL_116"))),
            (g, c, x) => x.IsSecret, fresh: true);
        Add("secret", "CuriousGlimmerroot decoys", (g, c) => {
                // The opponent deck holds a few class cards; the two decoys come from that class's pool.
                var offered = Offer(g, c, SpecificTask.CuriousGlimmerroot, Entity.FromCard(c, Cards.FromId("UNG_035"))).ToArray();
                var deck = c.Opponent.DeckCards.ToHashSet();
                return offered.Where(x => !deck.Contains(x)).ToArray();
            },
            (g, c, x) => Cls(x, c.Opponent.BaseClass), fresh: true);
        Add("secret", "RenonunceDarkness replacement cards", (g, c) => {
                var before = g.IdEntityDic.Values.Select(e => e.Id).ToHashSet();
                try { new SpecificTask.RenonunceDarkness().Process(g, c, Entity.FromCard(c, Cards.FromId("OG_118")), null, new TaskStack()); } catch (Exception) { }
                return g.IdEntityDic.Values.Where(e => !before.Contains(e.Id) && (e.Zone?.Type == Zone.HAND || e.Zone?.Type == Zone.DECK) && e is IPlayable && e.Card.Collectible && e.Card.Type != CardType.HERO_POWER && e.Card.Type != CardType.ENCHANTMENT)
                    .Select(e => e.Card).ToArray();
            },
            (g, c, x) => x.Collectible, fresh: true);
        // DeckZone.Fill (tests-only helper): fill an empty deck from the class pool.
        Add("deck", "DeckZone.Fill", (g, c) => c.DeckCards.ToArray(), (g, c, x) => ClsOrNeutral(x, c.HeroClass), fresh: true);
        return rows;
    }

    internal static void Run(params string[] groups) => Run(false, groups);

    internal static void Run(bool quick, params string[] groups)
    {
        var enabled = groups.Length == 0 ? DefaultGroups.ToHashSet() : groups.ToHashSet();
        var manifest = new Game(new GameConfig { RandomSeed = 1, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList() });
        manifest.StartGame();
        var pools = manifest.OfflinePools;
        var members = pools.Standard.ToDictionary(m => m.CardId);
        var skip = Environment.GetEnvironmentVariable("SAMPLER_SKIP"); // fail-before runs skip the shapes that hang on unmigrated code
        var rows = Rows(members).Where(r => enabled.Contains(r.Group) && (string.IsNullOrEmpty(skip) || !r.Name.Contains(skip))).ToList();
        // The default flow keeps it quick; --match-random-sampler-only runs the full count.
        int seedsLight = quick ? 30 : 60, seedsHeavy = quick ? 12 : 25;
        var violations = new SortedDictionary<string, (int Count, List<string> Examples)>();
        var outside = new SortedDictionary<string, int>();
        int generated = 0, runs = 0, empty = 0;
        string current = ""; long started = Environment.TickCount64;
        // A row that never returns is a hang in the task under test: say which one instead of stalling silently.
        bool finished = false;
        var watchdog = new Thread(() => {
            while (!Volatile.Read(ref finished)) { Thread.Sleep(5000); if (!Volatile.Read(ref finished) && Environment.TickCount64 - started > 20000) { Console.Error.WriteLine("  STUCK in: " + current); Console.Error.Flush(); } }
        }) { IsBackground = true };
        watchdog.Start();
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            // Light rows share a game per (seed, format); heavy rows get a fresh game each.
            for (int seed = 1; seed <= seedsLight; seed++)
            {
                var hero = HeroClasses[seed % HeroClasses.Length];
                var shared = rows.Any(r => !r.Fresh) ? NewGame(seed, format, hero) : null;
                foreach (var row in rows)
                {
                    if (row.Fresh && seed > seedsHeavy) continue;
                    Game g;
                    if (row.Fresh)
                    {
                        // Glimmerroot needs class cards in the opponent's deck; the fill test needs empty decks.
                        var opp = row.Name.StartsWith("Curious") ? new[] { "CS2_029", "CS2_022", "EX1_277" }.Select(Cards.FromId).ToArray() : null;
                        g = row.Name.StartsWith("Curious")
                            ? NewGame(seed, format, hero == CardClass.MAGE ? CardClass.PRIEST : hero, CardClass.MAGE, Enumerable.Range(0, 30).Select(i => opp[i % 3]).ToList())
                            : row.Group == "deck" ? NewGame(seed, format, hero, deck: new List<Card>(), fill: true)
                            : row.Name.StartsWith("Renonunce") ? NewGame(seed, format, CardClass.WARLOCK, deck: Enumerable.Repeat(Cards.FromId("CS2_059"), 30).ToList())
                            : NewGame(seed, format, hero);
                    }
                    else g = shared;
                    var c = g.Player1;
                    runs++; current = $"{row.Name} seed={seed} {format}"; started = Environment.TickCount64;
                    IEnumerable<Card> cards;
                    try { cards = row.Run(g, c).ToArray(); }
                    catch (Exception e) when (row.Fresh) { Note(violations, row.Name, $"threw {e.GetType().Name} seed={seed}"); continue; }
                    int count = 0;
                    foreach (var card in cards)
                    {
                        count++; generated++;
                        string label = $"{card.Id} {card.Name} (cost {card.Cost}, {card.Type}) {format} hero={c.HeroClass} seed={seed}";
                        if (!members.ContainsKey(card.Id)) { outside.TryGetValue(card.Id, out int n); outside[card.Id] = n + 1; Note(violations, row.Name, "outside pool: " + label); }
                        else if (!row.Ok(g, c, card)) Note(violations, row.Name, "filter: " + label);
                    }
                    if (count == 0) empty++;
                }
            }
        }
        Volatile.Write(ref finished, true);
        Console.WriteLine($"  sampler: {rows.Count} shapes in groups [{string.Join(",", enabled.OrderBy(x => x))}], {runs} runs, {generated} generated cards, {empty} empty runs");
        if (violations.Count != 0)
        {
            foreach (var v in violations) Console.WriteLine($"  VIOLATION {v.Key}: {v.Value.Count}x e.g. {string.Join(" | ", v.Value.Examples)}");
            Console.WriteLine($"  distinct cards outside the Standard pool: {outside.Count}: {string.Join(", ", outside.Take(25).Select(p => p.Key + "x" + p.Value))}");
            throw new Exception($"match random sampler: {violations.Count} task shapes generated cards outside the pool or against their filter");
        }
        Console.WriteLine("PASS match random sampler: every generated card is in the Standard pool and matches its filter");
        if (enabled.Contains("minion") && string.IsNullOrEmpty(skip)) { SourceExclusion(members); PerCard(members, enabled.Contains("format")); }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("match random sampler: " + message);
    }

    // The per-card checks alone (`--match-random-percard-only`): the named cards, played for real.
    internal static void PerCardOnly()
    {
        var game = NewGame(1, FormatType.FT_WILD, CardClass.MAGE);
        PerCard(game.OfflinePools.Standard.ToDictionary(m => m.CardId), true);
    }

    // A summon, transform or generated card never rolls its own source (modern code's rule, owner decision 2026-11-16).
    // Positive control first: with another source the same card does come up, so the absence below is not luck.
    private static void SourceExclusion(Dictionary<string, OfflinePoolPolicy.Member> members)
    {
        foreach (var legacy in new[] { false, true })
        {
            // Legacy lists hold the old printing of the Shredder; a record from before the policy replays those lists.
            var shredder = Cards.FromId(legacy ? "GVG_114" : "CORE_GVG_114");
            int cost = shredder.Cost;
            var g = NewGame(7, FormatType.FT_WILD, CardClass.MAGE, unversioned: legacy);
            var c = g.Player1;
            var self = Entity.FromCard(c, shredder);
            var other = Entity.FromCard(c, Cards.FromId("CS2_231"));
            int selfRolls = 0, controlRolls = 0;
            const int n = 1500;
            for (int i = 0; i < n; i++)
            {
                foreach (var (src, isSelf) in new[] { (self, true), (other, false) })
                {
                    var stack = new TaskStack { Number = cost };
                    new RandomMinionNumberTask(GameTag.COST).Process(g, c, src, null, stack);
                    var rolled = new List<Card>(stack.Playables?.Select(p => p.Card) ?? Enumerable.Empty<Card>());
                    rolled.AddRange(Gen(g, c, new RandomMinionTask(GameTag.COST, cost), src));
                    var minion = (Minion)Entity.FromCard(c, Cards.FromId("CS2_201"), null, c.BoardZone);
                    var tstack = new TaskStack { Playables = new List<IPlayable> { minion } };
                    new TransformMinionTask(EntityType.STACK, cost - Cards.FromId("CS2_201").Cost).Process(g, c, src, null, tstack);
                    rolled.AddRange(c.BoardZone.Select(m => m.Card)); foreach (var m in c.BoardZone.ToArray()) c.BoardZone.Remove(m);
                    int hits = rolled.Count(x => x.Id == shredder.Id);
                    if (isSelf) selfRolls += hits; else controlRolls += hits;
                }
            }
            Check(controlRolls > 0, $"control: another source rolls the Shredder (legacy={legacy}); the bucket is not empty");
            if (legacy) Check(selfRolls > 0, $"legacy replay keeps the old behaviour: the source can roll itself ({selfRolls})");
            else Check(selfRolls == 0, $"the Shredder rolled itself {selfRolls} times of {3 * n} summon/transform picks");
        }
        var generating = Entity.FromCard(NewGame(3, FormatType.FT_WILD, CardClass.MAGE).Player1, Cards.FromId("CORE_EX1_189"));
        var withSource = RandomCardTask.GetCardList(generating, CardType.MINION, rarity: Rarity.LEGENDARY);
        Check(!withSource.Any(x => x.Id == "CORE_EX1_189") && withSource.Count > 50, "a generated legendary never rolls its own source");
        var withOther = RandomCardTask.GetCardList(NewGame(3, FormatType.FT_WILD, CardClass.MAGE).Player1.Hero, CardType.MINION, rarity: Rarity.LEGENDARY);
        Check(withOther.Any(x => x.Id == "CORE_EX1_189"), "control: another source can roll it");
        Console.WriteLine("PASS source exclusion: summon, transform and generate never roll their own source (positive control present; legacy replay keeps old rolls)");
    }

    // The cards the issue names, played for real.
    private static void PerCard(Dictionary<string, OfflinePoolPolicy.Member> members, bool formatCards)
    {
        int outside = 0, total = 0;
        // Sneed's Old Shredder: 300 deathrattles in Standard-policy games under live (FT_WILD) and FT_STANDARD formats.
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        for (int seed = 1; seed <= 150; seed++)
        {
            var g = NewGame(seed, format, HeroClasses[seed % HeroClasses.Length]);
            var c = g.Player1;
            var shredder = (Minion)Entity.FromCard(c, Cards.FromId("CORE_GVG_114"));
            Generic.SummonBlock(g, shredder, -1, null); g.DeathProcessingAndAuraUpdate();
            shredder.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            foreach (var m in c.BoardZone)
            {
                total++;
                if (!members.ContainsKey(m.Card.Id) || m.Card.Rarity != Rarity.LEGENDARY) { outside++; Console.WriteLine($"  Sneed's summoned {m.Card.Id} {m.Card.Name} seed={seed} {format}"); }
            }
        }
        Check(total >= 290 && outside == 0, $"Sneed's: {outside} of {total} summons outside the Standard pool or not Legendary");
        // Astromancer: hand sizes 0..9 before the battlecry; the summon costs the remaining hand size and is Standard.
        int astro = 0;
        for (int hand = 0; hand <= 9; hand++)
        for (int seed = 1; seed <= 12; seed++)
        {
            var g = NewGame(seed, FormatType.FT_WILD, HeroClasses[seed % HeroClasses.Length]);
            var c = g.Player1;
            foreach (var h in c.HandZone.ToArray()) c.HandZone.Remove(h);
            for (int i = 0; i < hand; i++) Generic.DrawCard(c, Cards.FromId("CS2_182"));
            var astromancer = Generic.DrawCard(c, Cards.FromId("CORE_BOT_256"));
            c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
            var task = c.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == astromancer.Id);
            Check(task != null && g.Process(task), "Astromancer play");
            var summoned = c.BoardZone.Where(m => m.Id != astromancer.Id).ToArray();
            Check(summoned.Length <= 1, "at most one summon");
            foreach (var m in summoned)
            {
                astro++;
                Check(members.ContainsKey(m.Card.Id) && m.Card.Cost == c.HandZone.Count, $"Astromancer with {c.HandZone.Count} cards in hand summoned {m.Card.Id} cost {m.Card.Cost}");
            }
        }
        Check(astro > 50, "Astromancer summoned something in most hand sizes (" + astro + ")");
        if (!formatCards) { Console.WriteLine($"PASS per-card: Sneed's {total} summons and Astromancer {astro} summons are all Standard"); return; }
        // The Eternal Hold (TIME_446) and Storm the Gates (TLC_EVENT_400) key on the pool policy, not FormatType.
        var standardIds = members.Keys.ToHashSet();
        int eternal = 0, storm = 0;
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        for (int seed = 1; seed <= 60; seed++)
        {
            var g = NewGame(seed, format, CardClass.WARLOCK);
            var hold = Entity.FromCard(g.Player1, Cards.FromId("TIME_446"));
            foreach (var card in SabberStoneCore.CardSets.Client.TimeTravelPastPools.EternalHoldOffers(g, g.Player1, hold)) { eternal++; Check(standardIds.Contains(card.Id), "Eternal Hold offered non-Standard " + card.Id + " " + format); }
            var storm1 = (Spell)Entity.FromCard(g.Player1, Cards.FromId("TLC_EVENT_400"));
            g.Player1.Choice = null;
            SabberStoneCore.CardSets.Client.BuildABeast.BeginStorm(storm1);
            var firstOffer = g.Player1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
            foreach (var card in firstOffer) { storm++; Check(standardIds.Contains(card.Id), "Storm the Gates first offer non-Standard " + card.Id + " " + format); }
        }
        Check(eternal > 100 && storm > 100, $"Eternal Hold ({eternal}) and Storm ({storm}) offered cards");
        // A Wild policy is the one place TIME_446 may add the Past demons.
        var wildGame = new Game(new GameConfig { RandomSeed = 4, StartPlayer = 1, Shuffle = false, SkipMulligan = true, Logging = false, History = true,
            FormatType = FormatType.FT_WILD, PoolFormat = GenerationFormat.Wild, Player1HeroClass = CardClass.WARLOCK, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList() });
        wildGame.StartGame();
        var pastSeen = false;
        for (int i = 0; i < 80 && !pastSeen; i++)
            pastSeen = SabberStoneCore.CardSets.Client.TimeTravelPastPools.EternalHoldOffers(wildGame, wildGame.Player1, Entity.FromCard(wildGame.Player1, Cards.FromId("TIME_446")))
                .Any(card => !standardIds.Contains(card.Id));
        Check(pastSeen, "Wild policy still adds the Past demons to Eternal Hold");
        Console.WriteLine($"PASS per-card: Sneed's {total} summons, Astromancer {astro} summons, Eternal Hold {eternal} and Storm the Gates {storm} offers are all Standard; Wild policy keeps Past demons");
    }

    private static void Note(SortedDictionary<string, (int Count, List<string> Examples)> table, string row, string example)
    {
        table.TryGetValue(row, out var entry);
        if (entry.Examples == null) entry.Examples = new List<string>();
        entry.Count++;
        if (entry.Examples.Count < 2) entry.Examples.Add(example);
        table[row] = entry;
    }
}
