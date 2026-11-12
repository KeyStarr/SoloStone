using System;
using System.Collections.Generic;
using System.Linq;

// WP4 owns this file: queue rules, band offsets, fixed field from Diamond 5, Legend numbers, head-to-head, runtime hooks.
// The synthetic world at the bottom is a copy of tests/ladder-rules/SyntheticNpcWorld.cs (this project cannot link it).
internal static class MatchmakerSuite
{
    private static NpcWorld world;
    private static LadderMatchmaker NewMatchmaker(QueueRules rules = null) => new LadderMatchmaker(world, rules, SyntheticNpcWorld.StubProfile);
    private static NpcDeckEntry Deck(string id) => world.Catalogs.Decks.Find(id);

    internal static void Run()
    {
        var rules = QueueRules.Default;
        Program.Require(rules.NoSameNpcWithin == 5 && rules.NoSameDeckWithin == 3 && rules.AggroWindow == 4, "queue rule defaults");
        var h2h = HeadToHead.For("npc.x", new[] { new PastEncounter("npc.x", "d", DeckArchetype.Aggro, "Won"),
            new PastEncounter("npc.x", "d", DeckArchetype.Aggro, "Lost"), new PastEncounter("npc.y", "d", null, "Won") });
        Program.Require(h2h.Label == "1-1" && h2h.Games == 2, "head-to-head label");
        var h2hTie = HeadToHead.For("a", new[] { new PastEncounter("a", "d", null, "Won"), new PastEncounter("a", "d", null, "Tied"),
            new PastEncounter("a", "d", null, "Abandoned") }, new[] { "a", "b", "a" });
        Program.Require(h2hTie.Label == "1-1-1" && h2hTie.NpcConcessions == 2, "head-to-head ties, abandons, concessions");

        world = SyntheticNpcWorld.World();
        BandOffsets();
        Determinism();
        FixedFieldIgnoresHiddenRating();
        HiddenRatingMovesTheTarget();
        LegendField();
        LegendNumbersNeverCollide();
        QueueRulesHoldOverTenThousandMatches();
        TinyWorld();
        Runtime();
        Rematch();
        NotablesWithoutLegendDeck();
        Console.WriteLine("matchmaker suite passed");
    }

    private static MatchRequest Request(long profile, int match, int star, double hidden, IEnumerable<PastEncounter> history = null, int? legend = null) =>
        new MatchRequest(profile, match, star, hidden, legend, history);

    private static void BandOffsets()
    {
        var r = QueueRules.Default;
        Program.Require(LadderMatchmaker.BandOffset(LadderBand.Bronze) == r.BronzeSilverOffset && LadderMatchmaker.BandOffset(LadderBand.Silver) == r.BronzeSilverOffset,
            "Bronze/Silver offset");
        Program.Require(LadderMatchmaker.BandOffset(LadderBand.Gold) == r.GoldPlatinumOffset && LadderMatchmaker.BandOffset(LadderBand.Platinum) == r.GoldPlatinumOffset,
            "Gold/Platinum offset");
        Program.Require(LadderMatchmaker.BandOffset(LadderBand.Diamond) == r.DiamondOffset && LadderMatchmaker.BandOffset(LadderBand.Legend) == 0, "Diamond offset");
        Program.Require(r.BronzeSilverOffset > r.GoldPlatinumOffset && r.GoldPlatinumOffset > 0 && r.DiamondOffset > 0, "offsets are positive and Bronze-Silver is the most generous");
        Program.Require(r.FieldOffset >= -20 && r.FieldOffset <= 0, "owner rule: the Diamond 5+ field offset stays in -20..0");
        Program.Require(Math.Abs(MatchmakerTuning.OffsetForWinRate(.675) - 127) < 1 && Math.Abs(MatchmakerTuning.OffsetForWinRate(.575) - 52.5) < 1
            && Math.Abs(MatchmakerTuning.OffsetForWinRate(.525) - 16.7) < 1, "spec equilibrium offsets from the #52 win-rate midpoints");
    }

    private static void Determinism()
    {
        var a = NewMatchmaker(); var b = NewMatchmaker();
        var history = new[] { new PastEncounter(world.All[10].Id, world.All[10].DeckIds[0], null, "Won") };
        foreach (int star in new[] { 0, 44, 87, 119, 134, 135, 149, 150 })
        {
            var request = Request(12345, 77, star, 1450, history, star == 150 ? 120 : (int?)null);
            var x = a.Choose(request); var y = b.Choose(request); var z = a.Choose(request);
            Program.Require(x.Encounter.Npc.Id == y.Encounter.Npc.Id && x.Encounter.Deck.Id == y.Encounter.Deck.Id
                && x.Encounter.Rating == y.Encounter.Rating && x.Encounter.MatchSeed == y.Encounter.MatchSeed && x.Target == y.Target,
                "same (seed,index) gives the same encounter across instances at star " + star);
            Program.Require(x.Encounter.Npc.Id == z.Encounter.Npc.Id && x.Encounter.Deck.Id == z.Encounter.Deck.Id, "repeat call is stable at star " + star);
            Program.Require(x.Encounter.MatchSeed >= 0, "match seed is non-negative");
        }
        var picks = new HashSet<string>();
        for (int m = 0; m < 40; m++) picks.Add(a.Next(Request(12345, m, 60, 1450)).Npc.Id);
        Program.Require(picks.Count > 15, "different match indexes give varied opponents (" + picks.Count + ")");
        var other = new HashSet<string>();
        for (int m = 0; m < 40; m++) other.Add(a.Next(Request(999, m, 60, 1450)).Npc.Id);
        Program.Require(!picks.SetEquals(other), "different profiles draw different opponents");
        Program.Require(a.Next(Request(1, 0, 10, 1400)).Reason == "hidden-rating" && a.Next(Request(1, 0, 140, 1400)).Reason == "fixed-field"
            && a.Next(Request(1, 0, 150, 1400)).Reason == "legend-field", "reasons per ladder section");
        var encounter = a.Next(Request(1, 55, 100, 1400));
        Program.Require(encounter.Rating == a.Legend.DriftedRating(encounter.Npc, 55), "encounter rating is the NPC's drifted rating");
    }

    // Diamond 5 and up: the result depends on star index, seed, index and history only.
    private static void FixedFieldIgnoresHiddenRating()
    {
        var mm = NewMatchmaker();
        foreach (int star in new[] { 135, 140, 149 })
            for (int m = 0; m < 120; m++)
            {
                var low = mm.Choose(Request(42, m, star, 1100)); var high = mm.Choose(Request(42, m, star, 1900));
                Program.Require(low.Encounter.Npc.Id == high.Encounter.Npc.Id && low.Encounter.Deck.Id == high.Encounter.Deck.Id
                    && low.Target == high.Target, "D5+ ignores the hidden rating (star " + star + ", match " + m + ")");
            }
        double mean = 0; int n = 300;
        for (int m = 0; m < n; m++) mean += mm.Next(Request(42, m, 142, 1500)).Rating;
        mean /= n;
        double expected = LadderRules.RatingForIndex(142) + mm.Rules.FieldOffset;
        Program.Require(Math.Abs(mean - expected) < 30, "fixed field centres on the star index (mean " + mean + " vs " + expected + ")");
    }

    private static void HiddenRatingMovesTheTarget()
    {
        var mm = NewMatchmaker();
        double Mean(double hidden, int star)
        {
            double sum = 0; int n = 300;
            for (int m = 0; m < n; m++) sum += mm.Next(Request(5, m, star, hidden)).Rating;
            return sum / n;
        }
        double weakBronze = Mean(1150, 10), strongBronze = Mean(1550, 10);
        Program.Require(strongBronze - weakBronze > 300, "a strong hidden rating meets tougher NPCs early (" + weakBronze + " -> " + strongBronze + ")");
        double offset = 1550 - strongBronze;
        Program.Require(Math.Abs(offset - LadderMatchmaker.BandOffset(LadderBand.Bronze)) < 25, "Bronze opponents sit BandOffset below the hidden rating (" + offset + ")");
        double gold = 1550 - Mean(1550, 70);
        Program.Require(Math.Abs(gold - LadderMatchmaker.BandOffset(LadderBand.Gold)) < 25, "Gold opponents sit BandOffset below the hidden rating (" + gold + ")");
    }

    private static void LegendField()
    {
        var field = new NpcLegendField(world);
        double legend = LadderRules.RatingForIndex(LadderRules.LegendIndex);
        foreach (int match in new[] { 0, 9, 10, 250, 1000 })
        {
            var members = field.Field(match);
            Program.Require(members.Count >= 150 && members.Count <= 300, "Legend field size " + members.Count + " at match " + match);
            Program.Require(members.All(npc => field.DriftedRating(npc, match) >= legend)
                && members.Count == world.All.Count(npc => NpcLegendField.IsLegendRating(field.DriftedRating(npc, match))),
                "the field is every NPC whose drifted rating is Legend");
            double previous = double.MaxValue;
            foreach (var npc in members)
            {
                double r = field.DriftedRating(npc, match);
                Program.Require(r <= previous, "field sorted by drifted rating"); previous = r;
                Program.Require(Math.Abs(r - npc.HomeRating) <= NpcLegendField.MaxDrift + 1e-9, "drift stays within the clamp");
            }
            // Numbers are monotone in rating: stronger player, smaller number.
            int last = int.MaxValue;
            for (double rating = 1400; rating <= 2000; rating += 2)
            {
                int number = field.LegendNumber(rating, match);
                Program.Require(number <= last && number >= 1 && number <= members.Count + 1, "Legend numbers are monotone in rating");
                last = number;
            }
            Program.Require(field.LegendNumber(5000, match) == 1 && field.LegendNumber(0, match) == members.Count + 1, "number extremes");
            Program.Require(field.LegendNumber(field.DriftedRating(members[99], match) + 1e-6, match) <= 100, "beating the 100th NPC is top 100");
        }
        Program.Require(field.DriftedRating(world.All[0], 3) == world.All[0].HomeRating, "no drift before the first step");
        var moved = world.All.Take(200).Count(npc => field.DriftedRating(npc, 500) != npc.HomeRating);
        Program.Require(moved > 150, "the field drifts with the player's match count (" + moved + "/200)");
        Program.Require(field.DriftedRating(world.All[5], 123) == new NpcLegendField(world).DriftedRating(world.All[5], 123)
            && field.DriftedRating(world.All[5], 123) == field.DriftedRating(world.All[5], 123), "drift is a pure function of (world, npc, match)");
        Program.Require(field.DriftedRating(world.All[5], 123) == field.DriftedRating(world.All[5], 129), "drift advances in steps of ten matches");
        var mm = NewMatchmaker();
        var lowPlayer = mm.Next(Request(3, 100, 150, 1500)); var topPlayer = mm.Next(Request(3, 100, 150, 2000));
        Program.Require(lowPlayer.Rating <= field.FieldRating(1, 100) && topPlayer.Rating >= lowPlayer.Rating, "Legend opponents follow the player's number");
        double sumLow = 0, sumTop = 0;
        for (int m = 0; m < 200; m++) { sumLow += mm.Next(Request(3, m, 150, 1500)).Rating; sumTop += mm.Next(Request(3, m, 150, 2000)).Rating; }
        Program.Require(sumTop / 200 > sumLow / 200 + 60, "a higher Legend position meets stronger NPCs");
    }

    // 2026-11-08: the player and an NPC opponent both showed Legend #29. In-match numbers come from the real field: the player's
    // LegendNumber and the opponent's OpponentLegendNumber (NativeUIProbe.ProjectMedal) must never match while both are Legend.
    private static void LegendNumbersNeverCollide()
    {
        var field = new NpcLegendField(world);
        double legend = LadderRules.RatingForIndex(LadderRules.LegendIndex);
        void Distinct(double npc, double player, int match, string label)
        {
            int me = field.LegendNumber(player, match), them = field.OpponentLegendNumber(npc, match, player);
            Program.Require(me != them && (player >= npc) == (me < them), label + ": player #" + me + " vs opponent #" + them);
        }
        // An NPC rated below Legend that drifts into it (shows a Legend medal as an opponent), and one that drifts out.
        int up = -1, down = -1; NpcIdentity riser = null, faller = null;
        for (int m = 0; m <= 10000 && (riser == null || faller == null); m += NpcLegendField.StepEvery)
            foreach (var npc in world.All)
            {
                bool home = npc.HomeRating >= legend, now = NpcLegendField.IsLegendRating(field.DriftedRating(npc, m));
                if (riser == null && !home && now) { riser = npc; up = m; }
                if (faller == null && home && !now) { faller = npc; down = m; }
            }
        Program.Require(riser != null && faller != null, "fixture: some NPC drifts into Legend and some out of it");
        double rise = field.DriftedRating(riser, up);
        Program.Require(field.Field(up).Contains(riser) && !field.Field(down).Contains(faller), "drifted rating decides field membership");
        // Review case: a Legend player just under the riser with no field NPC between them (was: both #N).
        double under = Math.Max(legend, field.Ranked(up).Where(r => r.Rating < rise).Select(r => r.Rating).DefaultIfEmpty(legend).Max());
        double between = (under + rise) / 2;
        Program.Require(between >= legend && between < rise, "fixture: player between Legend and the riser");
        Distinct(rise, between, up, "opponent drifted into Legend, above the player");
        int tiedAtRise = field.Field(up).Count(npc => field.DriftedRating(npc, up) == rise);
        Program.Require(field.OpponentLegendNumber(rise, up, between) == field.LegendNumber(rise, up)
            && field.LegendNumber(between, up) == field.LegendNumber(rise, up) + tiedAtRise, "the riser takes the place right above the player");
        Distinct(rise, rise, up, "riser tied with the player");
        Distinct(rise, rise + 1, up, "riser below the player");
        // In-field opponents above, tied with and below the player, then every Legend NPC against ratings around it.
        var top = field.Field(0);
        double mid = field.DriftedRating(top[top.Count / 2], 0);
        Distinct(mid, mid - 5, 0, "in-field opponent above the player");
        Distinct(mid, mid, 0, "in-field opponent tied with the player");
        Distinct(mid, mid + 5, 0, "in-field opponent below the player");
        foreach (int m in new[] { 0, up, down })
            foreach (var npc in field.Field(m))
            {
                double r = field.DriftedRating(npc, m);
                foreach (double p in new[] { r - 1e-6, r, r + 1e-6, legend, legend - 30, 5000 }) Distinct(r, p, m, npc.Id + " at match " + m);
            }
        // Backstop: a stale rating outside the field (saved under another world) right above the player still gets its own number.
        double stale = (under + rise) / 2 + 1e-9;
        Program.Require(field.LegendNumber(stale, up) == field.LegendNumber(between, up) && field.OpponentLegendNumber(stale, up, between) != field.LegendNumber(between, up),
            "an out-of-field opponent rating never shares the player's number");
        // End to end: Legend picks and rematches carry the drifted rating of a field member at the request's match index (the
        // pending NpcRating ProjectMedal numbers at RankedHistory.Count), so their numbers never collide with the player's.
        var mm = NewMatchmaker();
        int legendPicks = 0;
        foreach (double hidden in new[] { legend - 20, legend + 1, between, 1700, 1850 })
            for (int m = 0; m < 3000; m += 37)
            {
                var pick = mm.Next(Request(55, m, LadderRules.LegendIndex, hidden, null, field.LegendNumber(hidden, m)));
                Program.Require(pick.Rating == mm.Legend.DriftedRating(pick.Npc, m), "a Legend pick carries the NPC's drifted rating");
                var rematch = mm.Rematch(Request(55, m + 1, LadderRules.LegendIndex, hidden,
                    new[] { new PastEncounter(pick.Npc.Id, pick.Deck.Id, pick.Deck.Tags.Archetype, "Lost") }, field.LegendNumber(hidden, m + 1)),
                    pick.Npc.Id, pick.Deck.Id, out _);
                foreach (var (encounter, at) in new[] { (pick, m), (rematch, m + 1) })
                {
                    if (encounter == null || !NpcLegendField.IsLegendRating(encounter.Rating)) continue;
                    legendPicks++;
                    Program.Require(encounter.Rating == mm.Legend.DriftedRating(encounter.Npc, at) && mm.Legend.Field(at).Contains(encounter.Npc),
                        "a Legend-medal opponent is a field member at its match index");
                    Distinct(encounter.Rating, hidden, at, encounter.Npc.Id + " served at match " + at);
                }
            }
        Program.Require(legendPicks > 300, "fixture: enough Legend-medal opponents (" + legendPicks + ")");
    }

    private static void QueueRulesHoldOverTenThousandMatches()
    {
        var mm = NewMatchmaker();
        var r = mm.Rules;
        var history = new List<PastEncounter>();
        var picks = new List<MatchPick>();
        int relaxed = 0, widened = 0;
        for (int m = 0; m < 10000; m++)
        {
            int star = (m / 60) % 151;
            double hidden = LadderRules.RatingForIndex(star) + 25 * Math.Sin(m);
            var request = Request(777, m, star, hidden, history.Skip(Math.Max(0, history.Count - r.HistoryDepth)));
            var pick = mm.Choose(request);
            var e = pick.Encounter;
            picks.Add(pick);
            int back = history.Count;
            // Independent re-check of every rule from the raw history.
            if (pick.RelaxLevel == 0)
            {
                for (int i = 1; i <= r.NoSameNpcWithin && i <= back; i++)
                    Program.Require(history[back - i].NpcId != e.Npc.Id, "no same NPC within " + r.NoSameNpcWithin + " (match " + m + ")");
                for (int i = 1; i <= r.NoSameDeckWithin && i <= back; i++)
                    Program.Require(history[back - i].DeckId != e.Deck.Id, "no same deck within " + r.NoSameDeckWithin + " (match " + m + ")");
                if (e.Deck.Tags.Archetype == DeckArchetype.Aggro)
                    for (int i = 1; i <= r.AggroWindow - 1 && i <= back; i++)
                        Program.Require(history[back - i].Archetype != DeckArchetype.Aggro, "at most one aggro deck per " + r.AggroWindow + " (match " + m + ")");
                if (e.Deck.Tags.Annoyance >= r.HighAnnoyance)
                    for (int i = 1; i <= r.HighAnnoyanceWindow - 1 && i <= back; i++)
                        Program.Require(Deck(history[back - i].DeckId).Tags.Annoyance < r.HighAnnoyance, "at most one high-annoyance deck per " + r.HighAnnoyanceWindow + " (match " + m + ")");
                int sameArchetype = 0;
                for (int i = 1; i <= r.ArchetypeWindow && i <= back; i++) if (history[back - i].Archetype == e.Deck.Tags.Archetype) sameArchetype++;
                Program.Require(sameArchetype < r.ArchetypeHardCap * r.ArchetypeWindow, "archetype share stays under the hard cap (match " + m + ")");
            }
            else
            {
                if (pick.RelaxLevel > (pick.RegularDraw ? 1 : 0)) relaxed++;   // the regular draw may drop the archetype cap by design (CastSuite measures it)
                // Even relaxed, the NPC floor holds.
                for (int i = 1; i <= r.MinNoSameNpcWithin && i <= back; i++)
                    Program.Require(history[back - i].NpcId != e.Npc.Id, "NPC rule never relaxes below " + r.MinNoSameNpcWithin + " (match " + m + ")");
            }
            if (pick.PoolWidened) widened++;
            Program.Require(e.Deck.Admitted && e.Deck.Tags.Allows(NpcWorld.BandForStarIndex(star)) && e.Npc.DeckIds.Contains(e.Deck.Id),
                "deck is admitted, allowed in the band and from the NPC's pool (match " + m + ")");
            history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, m % 2 == 0 ? "Won" : "Lost"));
        }
        Program.Require(relaxed < 100, "relaxation is rare in a full world (" + relaxed + "/10000)");
        Program.Require(widened == 0, "the nearest-24 pool always suffices in a full world (" + widened + ")");
        // Aggregate meta spread: no archetype dominates the long queue (retail often has 30%+).
        foreach (var group in picks.GroupBy(p => p.Encounter.Deck.Tags.Archetype))
            Program.Require(group.Count() / 10000.0 < 0.30, group.Key + " share " + group.Count() / 10000.0 + " stays below 30%");
        Program.Require(picks.Select(p => p.Encounter.Npc.Id).Distinct().Count() > 800, "the queue meets a wide cast");
        double highAnnoyance = picks.Count(p => p.Encounter.Deck.Tags.Annoyance >= r.HighAnnoyance) / 10000.0;
        Program.Require(highAnnoyance < 1.0 / r.HighAnnoyanceWindow + 0.02, "high-annoyance share within the budget (" + highAnnoyance + ")");
    }

    // A 3-NPC world (like the golden notables) must never throw and must keep the NPC floor.
    private static void TinyWorld()
    {
        var full = world;
        var library = full.Catalogs.Decks;
        var three = new List<NpcIdentity>();
        foreach (double rating in new[] { 1680.0, 1500.0, 1300.0 })
        {
            var band = NpcWorld.BandForRating(rating);
            var source = library.ForBand(band).Take(2).Select(d => d.Id).ToList();
            three.Add(new NpcIdentity("npc.golden." + (three.Count + 1), 1, "Golden " + three.Count, null, true, rating, "pro", DeckTaste.Netdecker,
                new string[0], source, null, null, 0, "t4"));
        }
        var tiny = new NpcWorld(1, 5, new NpcWorldCatalogs(library, full.Catalogs.Skills, full.Catalogs.Personalities, three, null, null), three);
        var mm = new LadderMatchmaker(tiny, null, SyntheticNpcWorld.StubProfile);
        var history = new List<PastEncounter>();
        for (int m = 0; m < 600; m++)
        {
            int star = (m * 7) % 151;
            var pick = mm.Choose(new MatchRequest(1, m, star, LadderRules.RatingForIndex(star), null, history));
            var e = pick.Encounter;
            if (history.Count >= 1) Program.Require(history[history.Count - 1].NpcId != e.Npc.Id || pick.RelaxLevel > 0 && tiny.All.Count < 3, "tiny world avoids an immediate rematch");
            if (history.Count >= 2) Program.Require(history[history.Count - 2].NpcId != e.Npc.Id, "tiny world keeps the NPC floor of two");
            history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, "Won"));
        }
        var elite = new NpcDeckLibrary(1, 1, "violet-hold", new[] { new NpcDeckEntry("elite-only", 1, "Elite", "WARRIOR", "HERO_01", "golden", "admitted", null,
            new NpcDeckTags(DeckArchetype.Control, 1, 1, 0, 3, LadderBand.Platinum, LadderBand.Legend), null, new NpcDeckCard[0], new NpcDeckSideboard[0]) });
        var eliteNpc = new NpcIdentity("npc.golden.9", 1, "Elite", null, true, 1650, "pro", DeckTaste.Netdecker, new string[0], new[] { "elite-only" }, null, null, 0, "t4");
        var eliteWorld = new NpcWorld(1, 5, new NpcWorldCatalogs(elite, full.Catalogs.Skills, full.Catalogs.Personalities, new[] { eliteNpc }, null, null), new[] { eliteNpc });
        var relaxedPick = new LadderMatchmaker(eliteWorld, null, SyntheticNpcWorld.StubProfile).Choose(new MatchRequest(1, 0, 0, 1000, null, null));
        Program.Require(relaxedPick.BandRelaxed && relaxedPick.Encounter.Npc.Id == "npc.golden.9", "a world without band-legal decks still yields an opponent");
        bool threw = false;
        try { new LadderMatchmaker(new NpcWorld(1, 1, new NpcWorldCatalogs(library, full.Catalogs.Skills, full.Catalogs.Personalities, new NpcIdentity[0], null, null), new NpcIdentity[0]), null, SyntheticNpcWorld.StubProfile)
            .Next(new MatchRequest(1, 0, 0, 1000, null, null)); }
        catch (InvalidOperationException) { threw = true; }
        Program.Require(threw, "an empty world reports a clear error");
    }

    // #28 (owner 2026-10-28): the last NPC and deck again while they stay a fair pick; harder NPCs always pass; 3 in a row at most.
    // A notable rated for Legend whose decks all stop below it: never served there, a rematch is refused, the leaderboard still counts it.
    // Oblivia is forced into that state so the test doesn't depend on which classes the shipped Legend list covers.
    private static void NotablesWithoutLegendDeck()
    {
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        string lowBandDeck = library.Admitted.First(d => !d.Tags.Allows(LadderBand.Legend)).Id;
        string notables = new System.Text.RegularExpressions.Regex("(\"id\":\\s*\"npc\\.n\\.oblivia\"[\\s\\S]*?\"decks\":\\s*\\[)[^\\]]*\\]")
            .Replace(Program.Data("notables-v1.json"), m => m.Groups[1].Value + "\"" + lowBandDeck + "\"]", 1);
        var catalogs = new NpcWorldCatalogs(library, SkillCatalog.Parse(Program.Data("skills-v1.json")), PersonalityCatalog.Parse(Program.Data("personalities-v1.json")),
            NpcNotables.Parse(notables), NpcNameTables.Parse(Program.Data("names-v1.json")), NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json")));
        var mm = new LadderMatchmaker(NpcWorld.Generate(catalogs), null, (npc, deck, personality, skill) => null);
        var field = mm.Legend.Field(0);
        var unreachable = new HashSet<string>(field.Where(n => !n.DeckIds.Any(id => mm.World.Catalogs.Decks.Find(id)?.Tags.Allows(LadderBand.Legend) == true)).Select(n => n.Id));
        Program.Require(unreachable.Contains("npc.n.oblivia"), "the forced notable has no Legend deck");
        int position = field.ToList().FindIndex(n => n.Id == "npc.n.oblivia") + 1;
        Program.Require(position >= 1, "the leaderboard counts a notable with no Legend deck");
        double oblivia = mm.Legend.FieldRating(position, 0);
        Program.Require(mm.Legend.LegendNumber(oblivia + 1000, 0) == 1 && mm.Legend.LegendNumber(oblivia - .001, 0) == position + 1,
            "a notable with no Legend deck still takes a Legend number");
        var history = new List<PastEncounter>();
        for (int m = 0; m < 2000; m++)
        {
            var e = mm.Next(Request(901, m, LadderRules.LegendIndex, oblivia, history, position));
            Program.Require(!unreachable.Contains(e.Npc.Id) && e.Deck.Tags.Allows(LadderBand.Legend), "served a Legend deck at Legend: " + e.Npc.Id + " " + e.Deck.Id);
            history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, "Won"));
            if (history.Count > 32) history.RemoveAt(0);
        }
        var fake = new[] { new PastEncounter("npc.n.oblivia", lowBandDeck, null, "Won") };
        Program.Require(mm.Rematch(Request(901, 1, LadderRules.LegendIndex, oblivia, fake, position), "npc.n.oblivia", lowBandDeck, out string refusal) == null
            && refusal == "deck not allowed in this band", "a rematch on a deck outside Legend is refused: " + refusal);
    }

    private static void Rematch()
    {
        var mm = NewMatchmaker();
        var rules = mm.Rules;
        string refusal;
        int allowed = 0;
        foreach (int star in new[] { 10, 70, 130, 140, 150 })
        {
            double hidden = star == 150 ? 1800 : 1450;
            var first = mm.Next(Request(77, 40, star, hidden));
            var past = new PastEncounter(first.Npc.Id, first.Deck.Id, first.Deck.Tags.Archetype, "Won");
            var request = Request(77, 41, star, hidden, new[] { past });
            double floor = mm.RematchFloor(request);
            var again = mm.Rematch(request, first.Npc.Id, first.Deck.Id, out refusal);
            bool fair = first.Deck.Tags.Allows(NpcWorld.BandForStarIndex(star)) && mm.Legend.DriftedRating(first.Npc, 41) >= floor - 1e-9;
            Program.Require((again != null) == fair && (again == null) == (refusal != null), "rematch follows the gate at star " + star + ": " + refusal);
            if (again == null) continue;
            allowed++;
            Program.Require(again.Npc.Id == first.Npc.Id && again.Deck.Id == first.Deck.Id && again.Reason == "rematch"
                && again.Rating == mm.Legend.DriftedRating(first.Npc, 41), "rematch keeps the NPC and deck at the new drifted rating, star " + star);
            var repeat = mm.Rematch(request, first.Npc.Id, first.Deck.Id, out refusal);
            Program.Require(repeat.MatchSeed == again.MatchSeed && again.MatchSeed >= 0
                && again.MatchSeed == mm.Next(request).MatchSeed, "rematch match seed is the index's normal seed");
        }
        // A fresh matchmaker pick is normally a fair rematch (its rating sits near the target).
        Program.Require(allowed >= 4, "fresh picks rematch (" + allowed + "/5)");

        // Floors: un-jittered target minus two sigmas; Diamond 5+ from the fixed field; Legend from the field 15 places below.
        Program.Require(Math.Abs(mm.RematchFloor(Request(1, 0, 10, 1500)) - (1500 - rules.BronzeSilverOffset - 2 * rules.JitterSigma)) < 1e-9, "Bronze floor");
        Program.Require(Math.Abs(mm.RematchFloor(Request(1, 0, 70, 1500)) - (1500 - rules.GoldPlatinumOffset - 2 * rules.JitterSigma)) < 1e-9, "Gold floor");
        Program.Require(Math.Abs(mm.RematchFloor(Request(1, 0, 130, 1500)) - (1500 - rules.DiamondOffset - 2 * rules.JitterSigma)) < 1e-9, "Diamond 10-6 floor");
        Program.Require(Math.Abs(mm.RematchFloor(Request(1, 0, 140, 1500)) - (LadderRules.RatingForIndex(140) + rules.FieldOffset - 2 * rules.FieldJitterSigma)) < 1e-9
            && mm.RematchFloor(Request(1, 0, 140, 1500)) == mm.RematchFloor(Request(1, 0, 140, 2200)), "Diamond 5+ floor ignores the hidden rating");
        int count = mm.Legend.Field(0).Count;
        Program.Require(mm.RematchFloor(Request(1, 0, 150, 1500, null, 20)) == mm.Legend.FieldRating(20 + rules.LegendSpread, 0)
            && mm.RematchFloor(Request(1, 0, 150, 1500, null, count)) == mm.Legend.FieldRating(count, 0), "Legend floor is 15 places below, clamped");

        // Legend: a player below the whole field gets the bottom field NPC as the floor; an empty field floors at the target.
        Program.Require(mm.RematchFloor(Request(1, 0, 150, 1000, null, count + 1)) == mm.Legend.FieldRating(count, 0), "below the whole field");
        var noField = new NpcWorld(1, world.WorldSeed, world.Catalogs, world.All.Where(n => n.HomeRating < LadderRules.RatingForIndex(LadderRules.LegendIndex)));
        var small = new LadderMatchmaker(noField, null, SyntheticNpcWorld.StubProfile);
        Program.Require(small.Legend.Field(0).Count == 0
            && small.RematchFloor(Request(1, 0, 150, 1500)) == small.TargetRating(Request(1, 0, 150, 1500), 1), "empty field floor is the target");
        Program.Require(mm.Rematch(Request(9, 30, 10, double.NaN, new[] { new PastEncounter(world.All[0].Id, world.All[0].DeckIds[0], null, "Won") }),
            world.All[0].Id, world.All[0].DeckIds[0], out refusal) == null, "a NaN hidden rating never passes the gate");

        // A stronger result moves the floor up: the same NPC becomes too weak; a harder NPC always passes.
        var bronze = world.All.Where(n => Deck(n.DeckIds[0]).Tags.Allows(LadderBand.Bronze)).OrderBy(n => n.HomeRating).ToList();
        var weak = bronze.First(n => n.HomeRating < 1000); var strong = bronze.Last();
        History(weak, out var weakPast);
        var tooWeak = Request(9, 30, 10, mm.Legend.DriftedRating(weak, 30) + rules.BronzeSilverOffset + 2 * rules.JitterSigma + 1, weakPast);
        Program.Require(mm.Rematch(tooWeak, weak.Id, weak.DeckIds[0], out refusal) == null && refusal == "opponent now too weak", "a weak NPC is refused: " + refusal);
        var atFloor = Request(9, 30, 10, mm.Legend.DriftedRating(weak, 30) + rules.BronzeSilverOffset + 2 * rules.JitterSigma, weakPast);
        Program.Require(mm.Rematch(atFloor, weak.Id, weak.DeckIds[0], out refusal) != null, "exactly at the floor is fair: " + refusal);
        History(strong, out var strongPast);
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, strongPast), strong.Id, strong.DeckIds[0], out refusal) != null, "a harder NPC always passes: " + refusal);

        // Series: three games in a row against one NPC at most; another opponent in between resets it.
        var p = new PastEncounter(strong.Id, strong.DeckIds[0], null, "Lost");
        var other = new PastEncounter("npc.other", "deck.other", null, "Won");
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { p, p }), strong.Id, strong.DeckIds[0], out refusal) != null, "game 3 of a series is allowed");
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { p, p, p }), strong.Id, strong.DeckIds[0], out refusal) == null
            && refusal == "series limit", "a fourth game in a row is refused: " + refusal);
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { p, p, other, p, p }), strong.Id, strong.DeckIds[0], out refusal) != null, "series counts consecutive games only");
        Program.Require(mm.Next(Request(9, 33, 10, 900, new[] { p, p, p })).Npc.Id != strong.Id, "normal matchmaking keeps the NPC away after a series");

        // Only the newest opponent, its own admitted deck, allowed in the current band.
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { p, other }), strong.Id, strong.DeckIds[0], out refusal) == null
            && refusal == "not the last opponent", "only the newest opponent: " + refusal);
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new PastEncounter[0]), strong.Id, strong.DeckIds[0], out refusal) == null, "no history, no rematch");
        string foreign = world.Catalogs.Decks.Admitted.First(d => !strong.DeckIds.Contains(d.Id)).Id;
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { new PastEncounter(strong.Id, foreign, null, "Won") }), strong.Id, foreign, out refusal) == null
            && refusal == "deck unavailable", "a deck outside the NPC's pool is refused: " + refusal);
        var ghost = new PastEncounter("npc.g.9999x", "synth-deck-00", null, "Won");
        Program.Require(mm.Rematch(Request(9, 30, 10, 900, new[] { ghost }), ghost.NpcId, ghost.DeckId, out refusal) == null && refusal == "unknown NPC", "unknown NPC: " + refusal);
        var banded = world.All.First(n => n.DeckIds.Any(id => Deck(id).Tags.MaxBand < LadderBand.Legend));
        string capped = banded.DeckIds.First(id => Deck(id).Tags.MaxBand < LadderBand.Legend);
        int above = LadderRules.LegendIndex;
        Program.Require(mm.Rematch(Request(9, 30, above, 5000, new[] { new PastEncounter(banded.Id, capped, null, "Won") }, 1), banded.Id, capped, out refusal) == null
            && refusal == "deck not allowed in this band", "a deck the new band does not allow is refused: " + refusal);
    }

    private static void History(NpcIdentity npc, out PastEncounter[] history) =>
        history = new[] { new PastEncounter(npc.Id, npc.DeckIds[0], null, "Won") };

    // The experimental Options switch (#63): off, Begin keeps the encounter object, so the search is the shipping one. On, only the
    // reply prior moves; a tier's research brain, node budgets, clocks, fixes and identity stay.
    private static void DeckInferenceSwitch(NpcEncounter plain, string deckId)
    {
        try
        {
            NpcWorldRuntime.InferredReplyPrior = () => false;
            NpcWorldRuntime.Begin(plain);
            Program.Require(ReferenceEquals(NpcWorldRuntime.Active, plain), "option off keeps the encounter itself");
            NpcWorldRuntime.InferredReplyPrior = null;
            NpcWorldRuntime.Begin(plain);
            Program.Require(ReferenceEquals(NpcWorldRuntime.Active, plain), "no option host keeps the encounter itself");
            NpcWorldRuntime.InferredReplyPrior = () => true;
            NpcWorldRuntime.Begin(plain);
            var live = NpcWorldRuntime.Active; var b = plain.Profile.Budget; var i = live.Profile.Budget;
            Program.Require(i.Brain.ReplyPrior == ReplyPrior.Inferred && plain.Profile.Budget.Brain == null && NpcWorldHooks.ProfileForDeck(deckId) == live.Profile && NpcCatalog.ForDeck(deckId) == live.Profile, "option on: the NPC seat resolves the inferred profile; the loaded encounter is untouched");
            Program.Require(b.TurnNodes == i.TurnNodes && b.PlanNodes == i.PlanNodes && b.LethalNodes == i.LethalNodes && b.ReplyK == i.ReplyK && b.ReplyL == i.ReplyL && b.ReplyR == i.ReplyR
                && b.OwnBeamWidth == i.OwnBeamWidth && b.Dedupe == i.Dedupe && b.TurnSearchSeconds == i.TurnSearchSeconds && b.TurnWallSeconds == i.TurnWallSeconds, "option on: no budget or clock changes");
            var p = plain.Profile; var q = live.Profile;
            Program.Require(p.Id == q.Id && p.Version == q.Version && p.DisplayName == q.DisplayName && p.DeckId == q.DeckId && p.HeroSkin == q.HeroSkin && p.HeroSkinPower == q.HeroSkinPower && p.CardBack == q.CardBack
                && ReferenceEquals(p.Personality, q.Personality) && p.PlanId == q.PlanId && ReferenceEquals(p.Fixes, q.Fixes) && live.Npc == plain.Npc && live.Deck == plain.Deck && live.Skill == plain.Skill
                && live.Personality == plain.Personality && live.Rating == plain.Rating && live.MatchSeed == plain.MatchSeed && live.Reason == plain.Reason, "option on: identity, personality, fixes and encounter facts stay");
            // Lists that do not build: the match still begins, on the shipping prior, and the host log says why.
            var population = NpcWorldHooks.Population; var logged = new List<string>();
            NpcWorldHooks.Population = () => new PopulationDeck[] { null };
            NpcWorldRuntime.DecorationLog = logged.Add;
            try
            {
                NpcWorldRuntime.Begin(plain);
                Program.Require(ReferenceEquals(NpcWorldRuntime.Active, plain) && logged.Count == 1 && logged[0].Contains("class prior for this match"), "broken inference lists fall back to the class prior and log it");
            }
            finally { NpcWorldHooks.Population = population; NpcWorldRuntime.DecorationLog = null; }
            NpcWorldRuntime.Begin(plain);
            Program.Require(NpcWorldRuntime.Active.Profile.Budget.Brain.ReplyPrior == ReplyPrior.Inferred, "the next match reads again once the lists build");
            var scales = new Dictionary<string, double> { { "hand", 1.5 } };
            var value = new LearnedValue("probe", Enumerable.Repeat(0.0, LearnedValue.FeatureNames.Count), 1);
            var deckScales = new Dictionary<string, IDictionary<string, double>> { { "d", new Dictionary<string, double> { { "taunt", 2 } } } };
            var brain = new BrainOptions(scales, .5, true, ReplyPrior.Class, .3, new[] { "x" }, value, .7, 2, 1, true, 3, true, deckScales);
            var moved = brain.WithReplyPrior(ReplyPrior.Inferred);
            Program.Require(moved.ReplyPrior == ReplyPrior.Inferred && moved.Scale("hand") == 1.5 && moved.ClearRisk == .5 && moved.ClearRiskByLife && moved.InferenceFloor == .3 && moved.ExcludedDecks.SequenceEqual(new[] { "x" })
                && moved.Value == value && moved.ValueBlend == .7 && moved.HandFlat == 2 && moved.HandTail == 1 && moved.SqrtLife && moved.ClearBonus == 3 && moved.Prune && moved.Scale("taunt", "d") == 2
                && brain.ReplyPrior == ReplyPrior.Class, "WithReplyPrior keeps every other brain field");
        }
        finally { NpcWorldRuntime.InferredReplyPrior = null; NpcWorldRuntime.End(); }
    }

    private static void Runtime()
    {
        NpcWorldRuntime.Uninstall();
        Program.Require(!NpcWorldRuntime.Installed && NpcWorldHooks.ProfileForDeck == null && NpcWorldRuntime.LegendNumber(1700, 0) == null
            && NpcWorldRuntime.PlanForId("auto:synth-deck-00") == null && NpcWorldRuntime.ChallengersFor("legend").Count == 0, "not installed => legacy behaviour");
        var mm = NewMatchmaker();
        NpcWorldRuntime.Install(world, mm);
        try
        {
            Program.Require(NpcWorldRuntime.Installed && NpcWorldHooks.ProfileForDeck != null && NpcWorldHooks.PlanForId != null
                && NpcWorldHooks.DecoratePilot != null && NpcWorldHooks.MulliganFor != null && ReferenceEquals(NpcWorldRuntime.Legend, mm.Legend), "Install wires every hook");
            Program.Require(NpcWorldRuntime.Active == null && NpcWorldHooks.ProfileForDeck("synth-deck-00") == null, "no active encounter => no world profile");
            Program.Require(NpcWorldRuntime.PlanForId("auto:no-such-deck") == null && NpcWorldRuntime.PlanForId("unknown.v1") == null, "unknown plan ids resolve to null");
            Program.Require(NpcWorldRuntime.LegendNumber(1700, 10) == mm.Legend.LegendNumber(1700, 10), "Legend number through the runtime");
            var npc = world.All[0]; var deck = Deck(npc.DeckIds[0]);
            var profile = new NpcProfile(npc.Id, 1, npc.DisplayName, deck.Id, "", 0, new Personality(0, 0, 0, 0, new EmoteProfile(0, 0), 0, 0, 99),
                new Budget(1, 1, 1, 1, 1, 1), deck.EffectivePlanId);
            var encounter = new NpcEncounter(npc, deck, world.Catalogs.Skills.Find("t7"), world.Catalogs.Personalities.Find("pro"), profile, 1700, 5, "fixed-field");
            NpcWorldRuntime.Begin(encounter);
            Program.Require(NpcWorldHooks.ProfileForDeck(deck.Id) == profile && NpcWorldHooks.ProfileForDeck("other") == null, "active encounter resolves its profile only");
            Program.Require(NpcWorldHooks.DecoratePilot(profile, null) == null, "skill without mistakes leaves the pilot unchanged");
            Program.Require(NpcWorldHooks.MulliganFor(profile, null) == null, "no greedy mulligan => default policy");
            NpcWorldRuntime.End();
            Program.Require(NpcWorldRuntime.Active == null && NpcWorldHooks.ProfileForDeck(deck.Id) == null, "End clears the active encounter");
            DeckInferenceSwitch(encounter, deck.Id);
            foreach (string milestone in new[] { "floor:silver-10", "floor:gold-10", "floor:platinum-10", "floor:diamond-10", "floor:diamond-5", "legend" })
            {
                var ids = NpcWorldRuntime.ChallengersFor(milestone);
                var band = milestone == "legend" ? LadderBand.Legend : (LadderBand)Enum.Parse(typeof(LadderBand), milestone.Substring(6, milestone.IndexOf('-') - 6), true);
                Program.Require(ids.Count > 0 && ids.Count <= NpcWorldRuntime.ChallengersPerMilestone, "challengers for " + milestone + ": " + ids.Count);
                Program.Require(ids.All(id => world.Find(id).Notable && NpcWorld.BandForRating(world.Find(id).HomeRating) == band), "challengers are notables of the reached band: " + milestone);
            }
            Program.Require(NpcWorldRuntime.ChallengersFor("legend:top-100").Count == 0 && NpcWorldRuntime.ChallengersFor(null).Count == 0, "non-band milestones grant no challengers");
        }
        finally { NpcWorldRuntime.Uninstall(); }
        Program.Require(NpcWorldHooks.ProfileForDeck == null && NpcWorldHooks.PlanForId == null && !NpcWorldRuntime.Installed, "Uninstall restores legacy behaviour");
    }
}


// Synthetic NPC world for the matchmaker tests and the world climb simulation: the spec's rating spread
// (85% piecewise-uniform over [920,1600) with league weights, 15% Legend field 1600 + Exp(70) capped at 1900),
// a deck library covering every archetype / annoyance / band, no engine data. Pure function of the seed.
internal static class SyntheticNpcWorld
{
    private static ulong Mix(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    private static double Unit(ref ulong state) { return (Mix(ref state) >> 11) * (1.0 / 9007199254740992.0); }

    public static NpcDeckLibrary Library(int deckCount = 60, long seed = 7)
    {
        var decks = new List<NpcDeckEntry>();
        ulong state = unchecked((ulong)seed);
        var archetypes = new[] { DeckArchetype.Aggro, DeckArchetype.Tempo, DeckArchetype.Midrange, DeckArchetype.Control, DeckArchetype.Combo };
        for (int i = 0; i < deckCount; i++)
        {
            double roll = Unit(ref state);
            int annoyance = roll < .5 ? 0 : roll < .75 ? 1 : roll < .9 ? 2 : 3;
            double minRoll = Unit(ref state), maxRoll = Unit(ref state);
            var min = minRoll < .7 ? LadderBand.Bronze : minRoll < .85 ? LadderBand.Silver : LadderBand.Gold;
            var max = maxRoll < .8 ? LadderBand.Legend : maxRoll < .9 ? LadderBand.Diamond : LadderBand.Platinum;
            var tags = new NpcDeckTags(archetypes[i % archetypes.Length], 1 + (int)(Unit(ref state) * 5), 1 + (int)(Unit(ref state) * 5),
                annoyance, 1 + (int)(Unit(ref state) * 5), min, max);
            decks.Add(new NpcDeckEntry("synth-deck-" + i.ToString("D2"), 1, "Synthetic " + i, "WARRIOR", "HERO_01", "golden", "admitted", null,
                tags, null, new NpcDeckCard[0], new NpcDeckSideboard[0]));
        }
        return new NpcDeckLibrary(1, 1, "violet-hold", decks);
    }

    public static SkillCatalog Skills()
    {
        var tiers = new List<SkillProfile>();
        for (int tier = 1; tier <= 7; tier++)
            tiers.Add(new SkillProfile("t" + tier, tier, 900 + (tier - 1) * 120, 900 + tier * 120, new Budget(1, 1, 1, 1, 1, 1), 0, 1, 1, null));
        return new SkillCatalog(1, tiers);
    }

    public static PersonalityCatalog Personalities() => new PersonalityCatalog(1, new[] {
        new PersonalityArchetype("pro", "Pro", .15, .45, .1, .3, .35, 1, 8, 0, 0, false, new EmoteProfile(0, 0)) });

    // generated + notables; HomeRating distribution as the spec's population section.
    public static NpcWorld World(int generated = NpcWorld.GeneratedCount, int notables = 40, long seed = 20261025)
    {
        var library = Library();
        var skills = Skills();
        ulong state = unchecked((ulong)seed ^ 0x57A1UL);
        var all = new List<NpcIdentity>();
        var leagueWeights = new[] { .15, .18, .20, .20, .27 };
        for (int i = 0; i < generated + notables; i++)
        {
            bool notable = i < notables;
            double rating;
            if (!notable && Unit(ref state) < .15) rating = Math.Min(1900, 1600 - 70 * Math.Log(1 - Unit(ref state)));
            else if (notable && i % 6 == 5) rating = 1600 + Unit(ref state) * 250;            // notables span every league
            else if (notable) rating = 1000 + (i % 6) * 120 + 5 + Unit(ref state) * 110;
            else
            {
                double pick = Unit(ref state), acc = 0; int league = 4;
                for (int l = 0; l < leagueWeights.Length; l++) { acc += leagueWeights[l]; if (pick < acc) { league = l; break; } }
                double low = league == 0 ? 920 : 1000 + league * 120, high = 1000 + (league + 1) * 120;
                rating = low + Unit(ref state) * (high - low);
            }
            var band = NpcWorld.BandForRating(rating);
            var pool = library.ForBand(band).ToList();
            int deckCount = 1 + (int)(Unit(ref state) * 3);
            var picked = new List<string>();
            for (int d = 0; d < deckCount && pool.Count > 0; d++)
            {
                int at = (int)(Unit(ref state) * pool.Count);
                picked.Add(pool[at].Id); pool.RemoveAt(at);
            }
            int tier = Math.Max(1, Math.Min(7, 1 + (int)((rating - 900) / 120)));
            string id = notable ? "npc.n.synth" + i.ToString("D3") : "npc.g." + i.ToString("D4");
            all.Add(new NpcIdentity(id, 1, "Synth " + id, notable ? "bio" : null, notable, rating, "pro", DeckTaste.Netdecker,
                new string[0], picked, null, null, 0, "t" + tier));
        }
        all.Sort((a, b) => { int c = b.HomeRating.CompareTo(a.HomeRating); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
        var catalogs = new NpcWorldCatalogs(library, skills, Personalities(), all.Where(n => n.Notable), null, null);
        return new NpcWorld(1, seed, catalogs, all);
    }

    // The profile stub for tests: the matchmaker only needs a builder, not WP3's real profile construction.
    public static NpcProfile StubProfile(NpcIdentity npc, NpcDeckEntry deck, PersonalityArchetype personality, SkillProfile skill) => null;
}
