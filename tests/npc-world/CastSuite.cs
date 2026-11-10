using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Named cast (#66): the shipped regulars and how often the real matchmakers draw them, on the real world data.
// `NpcWorldTests --cast-report [profiles] [climbs]` prints the full tables; Run() checks a smaller sample.
internal static class CastSuite
{
    // Hidden: an explicit hidden rating; null is the rating typical of the rank. New saves lose their way down fast (7 losses reach about 850).
    // The Legend list has no Rogue or Warlock deck (2026-11-06, may change after the owner's playtest). These regulars keep their
    // decks and their personality, so they have fewer at home; WhenPigsFly plays only below Legend. TempoTeddy's only other
    // Demon Hunter deck at Legend, Quest DH, is still provisional.
    private const int EliteFrom = 1700;
    private static readonly System.Collections.Generic.Dictionary<string, int> LegendListExceptions = new System.Collections.Generic.Dictionary<string, int>
    {
        ["CoinFlipCarl"] = 1, ["FelFlexx"] = 1, ["TempoTeddy"] = 1, ["WhenPigsFly"] = 0,
    };

    private static readonly (string Label, int Star, int? Legend, double? Hidden)[] Positions =
    {
        ("New save after early losses", 2, null, 850), ("Bronze", 15, null, null), ("Silver", 45, null, null), ("Gold", 75, null, null), ("Platinum", 105, null, null),
        ("Diamond 10-6", 128, null, null), ("Diamond 5-1", 142, null, null),
        ("Legend #200", 150, 200, null), ("Legend #100", 150, 100, null), ("Legend #30", 150, 30, null), ("Legend #5", 150, 5, null),
    };

    private sealed class Sample
    {
        public int Games, Regulars, Draws, RegularsSeen; public double MedianGap, MeanOffset; public int RematchRefused, RematchTried, Relaxed;
        public string TopDeck, TopArchetype;
        public double Share => 100.0 * Regulars / Games;
    }

    private static NpcWorld Real(bool casual = false)
    {
        var decks = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        if (casual) decks = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json")).MergeInto(decks);
        return NpcWorld.Generate(new NpcWorldCatalogs(decks, SkillCatalog.Parse(Program.Data("skills-v1.json")),
            PersonalityCatalog.Parse(Program.Data("personalities-v1.json")), NpcNotables.Parse(Program.Data("notables-v1.json")),
            NpcNameTables.Parse(Program.Data("names-v1.json")), NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json"))));
    }

    internal static void Run()
    {
        var world = Real();
        Data(world);
        Determinism(world);
        LegendList(world);
        Difficulty(world);
        var rules = QueueRules.Default;
        foreach (var position in Positions)
        {
            var s = Stationary(world, rules, position.Star, position.Legend, position.Hidden, 8, 150);
            string where = position.Label + " " + s.Share.ToString("F1", CultureInfo.InvariantCulture) + "%";
            Program.Require(s.Share >= 50 && s.Share <= 80, "regular share near 65%: " + where);
            Program.Require(s.MedianGap >= 7 && s.MedianGap <= 18, "a given regular comes back about every 8-15 games (small sample, 7-18 allowed): " + where + " gap " + s.MedianGap);
            if (position.Star < LadderRules.LegendIndex && position.Hidden == null) Program.Require(s.Relaxed * 20 <= s.Games, "queue rules rarely relax below Legend: " + where + " relaxed " + s.Relaxed + "/" + s.Games);
            Program.Require(s.RematchRefused * 10 <= s.RematchTried, "a regular just met can usually be rematched: " + where + " refused " + s.RematchRefused + "/" + s.RematchTried);
        }
        var none = new QueueRules { RegularShare = 0 };
        var withoutRegulars = Stationary(world, none, 75, null, null, 4, 150);
        Program.Require(withoutRegulars.Draws == 0 && withoutRegulars.Share < 10, "RegularShare 0 is the old queue");
        Casual(Real(true), 4, 150, true);
        Forecast(world);
        Console.WriteLine("cast suite passed");
    }

    internal static void Report(int profiles, int climbs)
    {
        var world = Real();
        var rules = QueueRules.Default;
        var control = new QueueRules { RegularShare = 0 };
        Console.WriteLine("| Position | Regular games % | Old queue % | Regulars met | Median games between meetings | Mean opponent minus target | Rematch refused | Rules relaxed % | Top deck | Top archetype |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (var p in Positions)
        {
            var s = Stationary(world, rules, p.Star, p.Legend, p.Hidden, profiles, 300);
            var old = Stationary(world, control, p.Star, p.Legend, p.Hidden, profiles, 300);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3} | {4:F0} | {5:+0.0;-0.0} (old {6:+0.0;-0.0}) | {7}/{8} | {9:F1} (old {10:F1}) | {11} (old {12}) | {13} (old {14}) |",
                p.Label, s.Share, old.Share, s.RegularsSeen, s.MedianGap, s.MeanOffset, old.MeanOffset, s.RematchRefused, s.RematchTried,
                100.0 * s.Relaxed / s.Games, 100.0 * old.Relaxed / old.Games, s.TopDeck, old.TopDeck, s.TopArchetype, old.TopArchetype));
        }
        Console.WriteLine();
        Casual(Real(true), profiles, 300, false);
        Console.WriteLine();
        Console.WriteLine("| Strength | Queue | Median games to Legend | p10-p90 | Win % Bronze-Silver / Gold-Platinum / Diamond / Legend | Regular games % | Regulars met per climb | Meetings with the most-met regular | Regular games within one rank of their home % |");
        Console.WriteLine("|---:|---|---:|---|---|---:|---:|---:|---:|");
        foreach (double strength in new[] { 1100.0, 1450.0, 1575.0, 1650.0 })
            foreach (var row in new[] { ("cast", rules), ("old", control) })
                Climb(world, row.Item2, row.Item1, strength, climbs);
    }

    // ---- shipped data ----
    private static void Data(NpcWorld world)
    {
        var notables = world.Catalogs.Notables.ToList();   // as written: the world drops unknown or unadmitted deck ids
        Program.Require(notables.Count == 60, "60 regulars (" + notables.Count + ")");
        var bands = notables.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)).ToDictionary(g => g.Key, g => g.Count());
        foreach (LadderBand band in new[] { LadderBand.Bronze, LadderBand.Silver, LadderBand.Gold, LadderBand.Platinum, LadderBand.Diamond })
            Program.Require(bands[band] == 8, "8 regulars in " + band + " (" + bands[band] + ")");
        Program.Require(bands[LadderBand.Legend] == 20, "20 regulars in Legend (" + bands[LadderBand.Legend] + ")");
        var library = world.Catalogs.Decks;
        foreach (var n in notables)
        {
            string who = n.Id + " " + n.DisplayName;
            var decks = n.DeckIds.Select(library.Find).ToList();
            Program.Require(decks.Count >= 2 && decks.Count <= 5, "2-5 decks: " + who);
            Program.Require(decks.All(d => d != null && d.Admitted), "only admitted decks: " + who);
            Program.Require(decks.Select(d => d.Id).Distinct().Count() == decks.Count, "no duplicate decks: " + who);
            Program.Require(decks.All(d => n.FavoriteClasses.Contains(d.Class)) && n.FavoriteClasses.All(c => decks.Any(d => d.Class == c)),
                "classes are exactly the deck classes: " + who);
            var band = NpcWorld.BandForRating(n.HomeRating);
            int atHome = LegendListExceptions.TryGetValue(n.DisplayName, out int fewer) ? fewer : 2;
            Program.Require(decks.Count(d => d.Tags.Allows(band)) >= atHome, "at least " + atHome + " decks playable at home rank: " + who);
            // Personality fit: the rules a reader would notice in play.
            var signature = decks[0].Tags;
            if (n.PersonalityId == "friendly") Program.Require(decks.All(d => d.Tags.Annoyance < 2), "friendly regulars bring no annoying decks: " + who);
            if (n.PersonalityId == "speedster") Program.Require(signature.Archetype != DeckArchetype.Control, "speedsters do not sign up for control: " + who);
            if (n.PersonalityId == "grinder") Program.Require(signature.Archetype != DeckArchetype.Aggro, "grinders do not sign up for aggro: " + who);
            if (n.Taste == DeckTaste.ClassLoyalist) Program.Require(n.FavoriteClasses.Count <= 2, "class loyalists stay in one class, two at most: " + who);
        }
    }

    // The Legend list exceptions really play where the data says they do.
    private static void LegendList(NpcWorld world)
    {
        var diamond = Met(world, 142, null);
        var legend = new HashSet<string>(new[] { 200, 100, 30, 5 }.SelectMany(number => Met(world, LadderRules.LegendIndex, number)));
        Program.Require(!legend.Contains("WhenPigsFly"), "WhenPigsFly holds no Legend deck, so never plays at Legend");
        Program.Require(diamond.Contains("WhenPigsFly"), "WhenPigsFly still plays at top Diamond");
        Program.Require(new[] { "CoinFlipCarl", "FelFlexx", "TempoTeddy" }.All(legend.Contains), "the one-deck regulars still play at Legend");
    }

    // Regulars' fixed tiers set most Legend games, and the climb model cannot see tiers, so nothing else notices if they drift.
    private static void Difficulty(NpcWorld world)
    {
        var skills = world.Catalogs.Skills;
        var order = skills.Tiers.Select(t => t.Id).ToList();
        foreach (var n in world.All.Where(npc => npc.Notable))
        {
            string who = n.Id + " " + n.DisplayName + " " + n.SkillId;
            if (n.HomeRating >= EliteFrom) Program.Require(n.SkillId == "elite", "regulars from " + EliteFrom + " up are elite: " + who);
            Program.Require(Math.Abs(order.IndexOf(n.SkillId) - order.IndexOf(skills.ForRating(n.HomeRating).Id)) <= 1,
                "a regular's tier stays within one of the rating rule: " + who);
        }
        var matchmaker = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        int games = 0, elite = 0;
        foreach (double rating in new[] { 1650.0, 1750, 1850 })
            for (int p = 0; p < 4; p++)
            {
                var history = new List<PastEncounter>();
                for (int m = 0; m < 300; m++)
                {
                    var e = matchmaker.Choose(new MatchRequest(1000 + p, m, LadderRules.LegendIndex, rating, null, history)).Encounter;
                    history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, m % 2 == 0 ? "Won" : "Lost"));
                    if (history.Count > 32) history.RemoveAt(0);
                    games++; if (e.Skill.Id == "elite") elite++;
                }
            }
        double share = 100.0 * elite / games;
        Program.Require(share >= 60 && share <= 76, "elite share at Legend near 68%: " + share.ToString("F1", CultureInfo.InvariantCulture));
    }

    private static HashSet<string> Met(NpcWorld world, int star, int? legend)
    {
        var matchmaker = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        double hidden = LadderRules.RatingForIndex(star) + QueueRules.Default.BandOffset(NpcWorld.BandForStarIndex(star));
        var met = new HashSet<string>(StringComparer.Ordinal);
        for (int p = 0; p < 8; p++)
        {
            var history = new List<PastEncounter>();
            for (int m = 0; m < 150; m++)
            {
                var e = matchmaker.Choose(new MatchRequest(6000 + p, m, star, hidden, legend, history.Skip(Math.Max(0, history.Count - QueueRules.Default.HistoryDepth)))).Encounter;
                history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, m % 2 == 0 ? "Won" : "Lost"));
                if (e.Npc.Notable) met.Add(e.Npc.DisplayName);
            }
        }
        return met;
    }

    private static void Determinism(NpcWorld world)
    {
        var a = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        var b = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        for (int m = 0; m < 200; m++)
        {
            var one = a.Choose(new MatchRequest(77, m, 75, 1310, null, null));
            var two = b.Choose(new MatchRequest(77, m, 75, 1310, null, null));
            Program.Require(one.Encounter.Npc.Id == two.Encounter.Npc.Id && one.Encounter.Deck.Id == two.Encounter.Deck.Id && one.RegularDraw == two.RegularDraw,
                "pick is a pure function of the request");
        }
    }

    // The live hook's forecast: the first pick is the real next match, later ones replay the queue, and nothing depends on a clock.
    private static void Forecast(NpcWorld world)
    {
        var matchmaker = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        var history = new[] { new PastEncounter("npc.n.salt-mine", "npc-rogue-mill", DeckArchetype.Control, "Lost") };
        var request = new MatchRequest(99, 7, 75, 1310, null, history);
        var picks = QueueForecast.Ranked(matchmaker, request, 40);
        Program.Require(picks.Count == 40 && picks[0].Encounter.Npc.Id == matchmaker.Choose(request).Encounter.Npc.Id, "forecast starts at the real next match");
        Program.Require(picks.Select(p => p.Encounter.Npc.Id).SequenceEqual(QueueForecast.Ranked(matchmaker, request, 40).Select(p => p.Encounter.Npc.Id)), "forecast is repeatable");
    }

    // ---- measurements ----
    private static Sample Stationary(NpcWorld world, QueueRules rules, int star, int? legend, double? fixedHidden, int profiles, int games)
    {
        var matchmaker = new LadderMatchmaker(world, rules, SyntheticNpcWorld.StubProfile);
        double hidden = fixedHidden ?? LadderRules.RatingForIndex(star) + rules.BandOffset(NpcWorld.BandForStarIndex(star));
        int depth = rules.HistoryDepth;
        var s = new Sample();
        var gaps = new List<int>(); var seen = new HashSet<string>(); double offset = 0;
        var deckCount = new Dictionary<string, int>(StringComparer.Ordinal); var archetypeCount = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int p = 0; p < profiles; p++)
        {
            var history = new List<PastEncounter>();
            var last = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int m = 0; m < games; m++)
            {
                var pick = matchmaker.Choose(new MatchRequest(5000 + p, m, star, hidden, legend, history.Skip(Math.Max(0, history.Count - depth))));
                var e = pick.Encounter;
                s.Games++; offset += e.Rating - pick.Target;
                if (pick.RegularDraw) s.Draws++;
                if (pick.RelaxLevel > 0) s.Relaxed++;
                int count;
                deckCount.TryGetValue(e.Deck.Name, out count); deckCount[e.Deck.Name] = count + 1;
                archetypeCount.TryGetValue(e.Deck.Tags.Archetype.ToString(), out count); archetypeCount[e.Deck.Tags.Archetype.ToString()] = count + 1;
                history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, m % 2 == 0 ? "Won" : "Lost"));
                if (!e.Npc.Notable) continue;
                s.Regulars++; seen.Add(e.Npc.Id);
                int previous;
                if (last.TryGetValue(e.Npc.Id, out previous)) gaps.Add(m - previous);
                last[e.Npc.Id] = m;
                if (pick.RegularDraw)
                {
                    string refusal;
                    s.RematchTried++;
                    if (matchmaker.Rematch(new MatchRequest(5000 + p, m + 1, star, hidden, legend, history.Skip(Math.Max(0, history.Count - depth))), e.Npc.Id, e.Deck.Id, out refusal) == null)
                        s.RematchRefused++;
                }
            }
        }
        gaps.Sort();
        Func<Dictionary<string, int>, string> top = d => { var best = d.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First();
            return best.Key + " " + (100.0 * best.Value / s.Games).ToString("F0", CultureInfo.InvariantCulture) + "%"; };
        s.TopDeck = top(deckCount); s.TopArchetype = top(archetypeCount);
        s.RegularsSeen = seen.Count; s.MedianGap = gaps.Count == 0 ? 0 : gaps[gaps.Count / 2]; s.MeanOffset = offset / s.Games;
        return s;
    }

    private static void Casual(NpcWorld world, int profiles, int games, bool check)
    {
        var catalog = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json"));
        if (!check)
        {
            Console.WriteLine("| Casual rating | Regular games % | Median games between meetings |");
            Console.WriteLine("|---:|---:|---:|");
        }
        foreach (double rating in new[] { 850.0, 1060.0, 1300.0, 1540.0, 1700.0 })
        {
            var matchmaker = new CasualMatchmaker(world, catalog, null, null, SyntheticNpcWorld.StubProfile);
            int total = 0, regulars = 0; var gaps = new List<int>();
            for (int p = 0; p < profiles; p++)
            {
                var history = new List<PastEncounter>();
                var last = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int m = 0; m < games; m++)
                {
                    var e = matchmaker.Choose(new CasualRequest(6000 + p, m, 0, rating, history.Skip(Math.Max(0, history.Count - 10)))).Encounter;
                    total++;
                    history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, "Won"));
                    if (!e.Npc.Notable) continue;
                    regulars++;
                    int previous;
                    if (last.TryGetValue(e.Npc.Id, out previous)) gaps.Add(m - previous);
                    last[e.Npc.Id] = m;
                }
            }
            gaps.Sort();
            double share = 100.0 * regulars / total; int gap = gaps.Count == 0 ? 0 : gaps[gaps.Count / 2];
            if (check) Program.Require(share >= 45 && share <= 80, "Casual regular share near 65% at " + rating + ": " + share.ToString("F1", CultureInfo.InvariantCulture));
            else Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "| {0:F0} | {1:F1} | {2} |", rating, share, gap));
        }
    }

    // A reference player of fixed strength climbs from a fresh position: win odds are the Elo expectation against the NPC's rating,
    // the ladder moves with the real LadderRules. Same model as tests/ladder-rules/WorldClimbSimulation.
    private static void Climb(NpcWorld world, QueueRules rules, string label, double strength, int climbs)
    {
        const int maximumGames = 1500, legendGames = 100;
        var matchmaker = new LadderMatchmaker(world, rules, SyntheticNpcWorld.StubProfile);
        var toLegend = new List<int>(); var games = new long[4]; var wins = new long[4];
        long total = 0, regulars = 0, metSum = 0, topSum = 0, homeRank = 0;
        for (int c = 0; c < climbs; c++)
        {
            var random = new Random(4242 + c);
            var pos = LadderRules.Fresh();
            var history = new List<PastEncounter>();
            var met = new Dictionary<string, int>(StringComparer.Ordinal);
            int legendAt = -1;
            for (int game = 0; game < maximumGames; game++)
            {
                if (pos.LegendReached && legendAt < 0) { legendAt = game; toLegend.Add(game); }
                if (legendAt >= 0 && game - legendAt >= legendGames) break;
                int? number = pos.LegendReached ? matchmaker.Legend.LegendNumber(pos.Rating, game) : (int?)null;
                var e = matchmaker.Next(new MatchRequest(StableSeed.Derive(4242, c), game, pos.StarIndex, pos.Rating, number, history.Skip(Math.Max(0, history.Count - rules.HistoryDepth))));
                bool win = random.NextDouble() < LadderRules.Expected(strength, e.Rating);
                int band = pos.StarIndex < 60 ? 0 : pos.StarIndex < 120 ? 1 : pos.StarIndex < LadderRules.LegendIndex ? 2 : 3;
                games[band]++; if (win) wins[band]++;
                total++;
                if (e.Npc.Notable)
                {
                    regulars++; int n; met.TryGetValue(e.Npc.Id, out n); met[e.Npc.Id] = n + 1;
                    if (Math.Abs((int)NpcWorld.BandForRating(e.Npc.HomeRating) - (int)NpcWorld.BandForStarIndex(pos.StarIndex)) <= 1) homeRank++;
                }
                pos = LadderRules.Apply(pos, win ? LadderOutcome.Win : LadderOutcome.Loss, e.Rating).After;
                history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, win ? "Won" : "Lost"));
            }
            metSum += met.Count; topSum += met.Count == 0 ? 0 : met.Values.Max();
        }
        toLegend.Sort();
        string median = toLegend.Count == 0 ? "never" : toLegend[toLegend.Count / 2].ToString(CultureInfo.InvariantCulture);
        string spread = toLegend.Count == 0 ? "-" : toLegend[toLegend.Count / 10] + "-" + toLegend[toLegend.Count * 9 / 10] + " (" + toLegend.Count + "/" + climbs + " reach)";
        string rates = string.Join(" / ", Enumerable.Range(0, 4).Select(b => games[b] == 0 ? "-" : (100.0 * wins[b] / games[b]).ToString("F1", CultureInfo.InvariantCulture)));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "| {0:F0} | {1} | {2} | {3} | {4} | {5:F1} | {6:F1} | {7:F1} | {8:F0} |",
            strength, label, median, spread, rates, 100.0 * regulars / total, (double)metSum / climbs, (double)topSum / climbs, regulars == 0 ? 0 : 100.0 * homeRank / regulars));
    }
}
