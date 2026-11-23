using System;
using System.Collections.Generic;
using System.Linq;

// NPC difficulty (owner 2026-11-15, NpcDifficulty.cs): Hard is today's matchmaker unchanged; Easy and Medium are the ladder
// shifted down with a Core step below Bronze and a beginner boost that fades with rank; Extreme is locked.
internal static class DifficultySuite
{
    private static NpcWorld Real(bool casual = false)
    {
        var decks = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        if (casual) decks = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json")).MergeInto(decks);
        return NpcWorld.Generate(new NpcWorldCatalogs(decks, SkillCatalog.Parse(Program.Data("skills-v1.json")),
            PersonalityCatalog.Parse(Program.Data("personalities-v1.json")), NpcNotables.Parse(Program.Data("notables-v1.json")),
            NpcNameTables.Parse(Program.Data("names-v1.json")), NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json"))));
    }

    private const long Profile = 424242;

    internal static void Run()
    {
        Levels();
        var world = Real();
        CoreDecks(world);
        var mm = new LadderMatchmaker(world);
        HardUnchanged(mm);
        HardPin.Run();
        ShiftScalesWithRank(mm, world);
        CoreStepAtBronze(mm);
        BeginnerServed(mm);
        BoostFadesAndKeepsFloors(world);
        Rematch(mm);
        CoreQueueRules(mm);
        DisplayCap();
        RealResolver();
        Casual();
        Console.WriteLine("difficulty suite passed");
    }

    private static void Levels()
    {
        Program.Require(NpcDifficulties.Find(null) == NpcDifficulties.Hard, "absent difficulty is Hard");
        Program.Require(NpcDifficulties.NewSaveDefault == NpcDifficulties.Medium, "new saves start at Medium");
        Program.Require(NpcDifficulties.All.Select(l => l.Id).SequenceEqual(new[] { "easy", "medium", "hard", "extreme" }), "level order");
        Program.Require(NpcDifficulties.Extreme.Locked && !NpcDifficulties.IsSelectable("extreme") && NpcDifficulties.IsKnown("extreme"), "Extreme locked");
        Program.Require(NpcDifficulties.IsSelectable("easy") && NpcDifficulties.IsSelectable("hard") && !NpcDifficulties.IsKnown("nightmare"), "selectable levels");
        bool threw = false;
        try { NpcDifficulties.Find("nightmare"); } catch (InvalidOperationException) { threw = true; }
        Program.Require(threw, "unknown difficulty refused");
        Program.Require(NpcDifficulties.Hard.IsHard && NpcDifficulties.Extreme.IsHard && !NpcDifficulties.Easy.IsHard, "Hard and locked Extreme change nothing");
        Program.Require(NpcDifficulties.Easy.ShiftStars == 60 && NpcDifficulties.Medium.ShiftStars == 30, "shifts: Easy two leagues, Medium one");
        Program.Require(NpcDifficulties.Easy.DisplayShift == 240 && NpcDifficulties.Hard.DisplayShift == 0, "display shift in rating points");
        Program.Require(NpcDifficulties.Easy.CoreChance(0) == 0 && NpcDifficulties.Easy.CoreChance(-15) == 0.5
            && NpcDifficulties.Easy.CoreChance(-60) == 1, "Core share rises below Bronze 10");
    }

    private static void CoreDecks(NpcWorld world)
    {
        var all = world.Catalogs.Decks.Decks.Where(NpcDifficultyShaper.IsCoreDeck).ToList();
        var core = all.Where(d => d.Origin == NpcCoreDecks.Origin).ToList();
        var beginner = all.Where(d => d.Origin == NpcBeginnerDecks.Origin).ToList();
        Program.Require(all.Count == 22 && core.Count == 11 && beginner.Count == 11, "11 Core starters plus 11 beginner decks: " + core.Count + "+" + beginner.Count);
        Program.Require(core.Select(d => d.Class).Distinct().Count() == 11 && beginner.Select(d => d.Class).Distinct().Count() == 11, "one of each per class");
        Program.Require(core.All(d => d.Id == NpcCoreDecks.IdFor(d.Class)) && beginner.All(d => d.Id == NpcBeginnerDecks.IdFor(d.Class)), "Core deck ids");
        Program.Require(all.All(d => !d.Admitted), "Core and beginner decks are never admitted ladder decks");
        Program.Require(world.All.All(npc => npc.DeckIds.All(id => !id.StartsWith(NpcCoreDecks.IdPrefix, StringComparison.Ordinal)
            && !id.StartsWith(NpcBeginnerDecks.IdPrefix, StringComparison.Ordinal))), "no NPC is generated with a Core or beginner deck");
        var problems = world.Catalogs.Decks.Validate();
        Program.Require(problems.Count == 0, "merged library validates: " + string.Join("; ", problems));
        var resolve = world.Catalogs.Decks.LocalDeckResolver(null, true, _ => true);
        foreach (var deck in core)
        {
            var local = resolve(deck.Id);
            Program.Require(local != null, "Core deck resolves: " + deck.Id);
            local.Build();
            var player = LocalDecks.PlayerCoreStarterDecks.Single(p => p.Hero == deck.Hero);
            Program.Require(local.CardIds.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(player.CardIds.OrderBy(c => c, StringComparer.Ordinal)),
                "Core NPC deck matches the player's Core starter: " + deck.Id);
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var deck in beginner)
        {
            var local = resolve(deck.Id);
            Program.Require(local != null, "beginner deck resolves: " + deck.Id);
            local.Build();
            var cards = local.CardIds.ToList();
            Program.Require(cards.Count == 30, "beginner deck has 30 cards: " + deck.Id);
            Program.Require(cards.All(id => id != "CS3_035"), "beginner deck has no Nozdormu: " + deck.Id);
            Program.Require(cards.GroupBy(id => id).All(g => g.Count() <= 2), "beginner deck copy limit: " + deck.Id);
            foreach (string id in cards.Distinct())
            {
                var card = SabberStoneCore.Model.Cards.FromId(id);
                Program.Require(card != null && card.Rarity != SabberStoneCore.Enums.Rarity.LEGENDARY, "beginner deck has no legendary: " + deck.Id + " " + id);
                string cls = card.Class.ToString();
                Program.Require(cls == deck.Class || cls == "NEUTRAL", "beginner deck card is its class or neutral: " + deck.Id + " " + id + " " + cls);
            }
            Program.Require(!LocalDecks.PlayerCoreStarterDecks.Any(p => p.Name.Equals(deck.Name, StringComparison.OrdinalIgnoreCase)) && names.Add(deck.Name),
                "beginner deck name is unique: " + deck.Name);
            var starter = LocalDecks.PlayerCoreStarterDecks.Single(p => p.Hero == deck.Hero);
            Program.Require(!local.CardIds.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(starter.CardIds.OrderBy(c => c, StringComparer.Ordinal)),
                "beginner deck differs from the Core starter: " + deck.Id);
        }
    }

    private static void BeginnerServed(LadderMatchmaker mm)
    {
        int CountBeginner(NpcDifficultyLevel level, double rating)
        {
            return Enumerable.Range(0, 200).Count(index =>
            {
                var deck = mm.Next(new MatchRequest(Profile + 5, index, 0, rating, null, null, level)).Deck;
                bool beginner = deck.Id == NpcBeginnerDecks.IdFor(deck.Class);
                Program.Require(beginner || deck.Id == NpcCoreDecks.IdFor(deck.Class),
                    "Core step serves a starter or beginner deck of the same class");
                return beginner;
            });
        }
        Program.Require(CountBeginner(NpcDifficulties.Easy, LadderRules.StartRating) == 200,
            "fresh Easy always serves beginner decks");
        int medium = CountBeginner(NpcDifficulties.Medium, LadderRules.StartRating);
        Program.Require(medium > 50 && medium < 120, "fresh Medium mixes starter and beginner decks: " + medium);
        Program.Require(CountBeginner(NpcDifficulties.Medium, 1100) == 200, "Medium offers beginner decks after rating falls");
        Program.Require(CountBeginner(NpcDifficulties.Medium, 1600) == 0, "Medium offers starter decks once rating rises");
    }

    private static IEnumerable<MatchRequest> Ladder(NpcDifficultyLevel level, int matches = 120)
    {
        int[] stars = { 0, 15, 45, 75, 105, 125, 140, 150 };
        for (int i = 0; i < matches; i++)
        {
            int star = stars[i % stars.Length];
            double hidden = LadderRules.RatingForIndex(star) + 30;
            yield return new MatchRequest(Profile, i, star, hidden, star >= LadderRules.LegendIndex ? 500 : (int?)null, null, level);
        }
    }

    private static void HardUnchanged(LadderMatchmaker mm)
    {
        var plain = Ladder(null).Select(r => mm.Choose(r)).ToList();
        var hard = Ladder(NpcDifficulties.Hard).Select(r => mm.Choose(r)).ToList();
        var extreme = Ladder(NpcDifficulties.Extreme).Select(r => mm.Choose(r)).ToList();
        for (int i = 0; i < plain.Count; i++)
            foreach (var other in new[] { hard[i], extreme[i] })
            {
                var a = plain[i].Encounter; var b = other.Encounter;
                Program.Require(a.Npc.Id == b.Npc.Id && a.Deck.Id == b.Deck.Id && ReferenceEquals(a.Skill, b.Skill) && a.Rating == b.Rating
                    && a.MatchSeed == b.MatchSeed && a.Reason == b.Reason && plain[i].Target == other.Target && b.Difficulty == null,
                    "Hard is today's matchmaker at match " + i);
            }
    }

    private static void ShiftScalesWithRank(LadderMatchmaker mm, NpcWorld world)
    {
        foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
        {
            double previous = double.MinValue;
            foreach (int star in new[] { 0, 45, 75, 105, 140, 150 })
            {
                var picks = Enumerable.Range(0, 60).Select(i => mm.Choose(new MatchRequest(Profile, i, star, LadderRules.RatingForIndex(star) + 30,
                    star >= LadderRules.LegendIndex ? 500 : (int?)null, null, level))).ToList();
                var hard = Enumerable.Range(0, 60).Select(i => mm.Choose(new MatchRequest(Profile, i, star, LadderRules.RatingForIndex(star) + 30,
                    star >= LadderRules.LegendIndex ? 500 : (int?)null, null))).ToList();
                double mean = picks.Average(p => p.Encounter.Rating), hardMean = hard.Average(p => p.Encounter.Rating);
                Program.Require(mean > previous, level.Id + " opponents get stronger with rank at star " + star);
                // At Bronze the population's floor (no NPC far below Bronze 10) caps the shift; the Core step and boost carry it there.
                double gap = star < 30 ? 0 : level.ShiftRating / 2;
                Program.Require(mean <= hardMean - gap, level.Id + " opponents weaker than Hard at star " + star + ": " + mean + " vs " + hardMean);
                previous = mean;
                var band = NpcWorld.BandForStarIndex(Math.Max(0, level.LadderPosition(star)));
                foreach (var pick in picks)
                {
                    var e = pick.Encounter;
                    Program.Require(e.Difficulty == level.Id && e.DifficultyPosition == level.LadderPosition(star), "encounter carries its level");
                    Program.Require(NpcDifficultyShaper.IsCoreDeck(e.Deck) || e.Deck.Tags.Allows(band) || pick.BandRelaxed,
                        level.Id + " deck from the shifted band at star " + star + ": " + e.Deck.Id);
                    Program.Require(e.Reason != "legend-field", "shifted Legend uses the fixed field");
                    Program.Require(e.Skill.Id.EndsWith("+" + level.Id, StringComparison.Ordinal) && e.Profile.Budget.PlanNodes == e.Skill.Budget.PlanNodes,
                        "boosted skill drives the profile");
                }
            }
        }
    }

    private static void CoreStepAtBronze(LadderMatchmaker mm)
    {
        var easy = Enumerable.Range(0, 300).Select(i => mm.Choose(new MatchRequest(Profile + 1, i, 5, 1030, null, null, NpcDifficulties.Easy)).Encounter).ToList();
        int core = easy.Count(e => NpcDifficultyShaper.IsCoreDeck(e.Deck));
        Program.Require(core >= 200, "Easy at Bronze is mostly Core decks: " + core + "/300");
        Program.Require(easy.Where(e => !NpcDifficultyShaper.IsCoreDeck(e.Deck)).All(e => e.Deck.Tags.PowerTier >= 4 && e.Deck.Tags.Archetype != DeckArchetype.Aggro),
            "non-Core decks at the Core step are soft and not Aggro");
        Program.Require(easy.Where(e => NpcDifficultyShaper.IsCoreDeck(e.Deck)).All(e => e.Profile.DeckId == e.Deck.Id && e.Npc.DeckIds.Count > 0),
            "Core deck profile names the Core deck");
        var medium = Enumerable.Range(0, 300).Select(i => mm.Choose(new MatchRequest(Profile + 1, i, 29, 1030, null, null, NpcDifficulties.Medium)).Encounter).ToList();
        Program.Require(medium.Count(e => NpcDifficultyShaper.IsCoreDeck(e.Deck)) < 30, "Medium at Bronze 1 has almost no Core decks");
        var gold = Enumerable.Range(0, 120).Select(i => mm.Choose(new MatchRequest(Profile + 1, i, 75, 1330, null, null, NpcDifficulties.Easy)).Encounter).ToList();
        Program.Require(gold.All(e => !NpcDifficultyShaper.IsCoreDeck(e.Deck)), "Easy at Gold has left the Core step");
    }

    private static void BoostFadesAndKeepsFloors(NpcWorld world)
    {
        var skills = world.Catalogs.Skills;
        var novice = skills.Tiers[0];
        foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
            foreach (var skill in skills.Tiers)
                foreach (int position in new[] { -60, 0, 45, 90, 150 })
                {
                    var boosted = level.Apply(skill, novice, position);
                    Program.Require(boosted.Mistakes.All(m => m.Chance > 0 && m.Chance <= NpcDifficultyLevel.MaxChance), "chances in (0, 0.6]");
                    Program.Require(boosted.Mistakes.Where(m => m.Kind == MistakeKind.LethalMiss).All(m => m.MinLethalActions >= SkillCatalog.MinMissableLethalActions),
                        "short lethals are always taken");
                    Program.Require(boosted.Budget.PlanNodes >= Math.Min(skill.Budget.PlanNodes, NpcDifficultyLevel.MinPlanNodes)
                        && boosted.Budget.PlanNodes <= skill.Budget.PlanNodes && boosted.Budget.LethalNodes == skill.Budget.LethalNodes,
                        "budget floor, never above the tier, lethal budget kept");
                    foreach (var own in skill.Mistakes)
                        Program.Require(boosted.Chance(own.Kind) >= Math.Min(NpcDifficultyLevel.MaxChance, own.Chance) - 1e-12, "boost never removes a tier mistake");
                }
        Program.Require(Close(NpcDifficulties.Easy.BoostAt(-30), NpcDifficulties.Easy.Bottom) && Close(NpcDifficulties.Easy.BoostAt(90), NpcDifficulties.Easy.Top),
            "Easy boost: full at the bottom, its top value at the top");
        var master = skills.Find("master");
        var mediumTop = NpcDifficulties.Medium.Apply(master, novice, LadderRules.LegendIndex - NpcDifficulties.Medium.ShiftStars);
        Program.Require(mediumTop.Mistakes.Count == master.Mistakes.Count && mediumTop.Budget.PlanNodes == master.Budget.PlanNodes
            && mediumTop.Temperature == master.Temperature, "Medium's boost has faded at its top");
        var easyBottom = NpcDifficulties.Easy.Apply(novice, novice, -30);
        Program.Require(easyBottom.Chance(MistakeKind.TradeTarget) > novice.Chance(MistakeKind.TradeTarget)
            && easyBottom.Chance(MistakeKind.IgnoreAoeRisk) >= 0.6 && easyBottom.Budget.PlanNodes == NpcDifficultyLevel.MinPlanNodes && easyBottom.Temperature >= 3,
            "Easy bottom: more mistakes, a quarter of the search at the 1750-node floor");
    }

    private static bool Close(DifficultyBoost a, DifficultyBoost b) => Math.Abs(a.MistakeScale - b.MistakeScale) < 1e-9
        && Math.Abs(a.BudgetScale - b.BudgetScale) < 1e-9 && Math.Abs(a.AoeFloor - b.AoeFloor) < 1e-9 && a.TopNFloor == b.TopNFloor;

    private static void Rematch(LadderMatchmaker mm)
    {
        // An Easy Bronze loss to a Core deck can be rematched on Easy (same Core deck, boosted skill), never on Hard.
        NpcEncounter lost = null; int index = -1;
        for (int i = 0; i < 200 && lost == null; i++)
        {
            var e = mm.Choose(new MatchRequest(Profile + 2, i, 5, 1030, null, null, NpcDifficulties.Easy)).Encounter;
            if (NpcDifficultyShaper.IsCoreDeck(e.Deck)) { lost = e; index = i; }
        }
        Program.Require(lost != null, "an Easy Core encounter exists");
        var history = new[] { new PastEncounter(lost.Npc.Id, lost.Deck.Id, lost.Deck.Tags.Archetype, "Lost") };
        var again = mm.Rematch(new MatchRequest(Profile + 2, index + 1, 5, 1030, null, history, NpcDifficulties.Easy), lost.Npc.Id, lost.Deck.Id, out string refusal);
        Program.Require(again != null && again.Deck.Id == lost.Deck.Id && again.Difficulty == "easy" && again.Skill.Id.EndsWith("+easy", StringComparison.Ordinal),
            "Easy rematch keeps the Core deck: " + refusal);
        var hard = mm.Rematch(new MatchRequest(Profile + 2, index + 1, 5, 1030, null, history), lost.Npc.Id, lost.Deck.Id, out refusal);
        Program.Require(hard == null && refusal == "deck not allowed in this band", "Hard never rematches a Core deck: " + refusal);
    }

    // Core decisions are made before candidates are built, so the queue rules judge the decks actually served.
    private static void CoreQueueRules(LadderMatchmaker mm)
    {
        foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
            foreach (int seed in new[] { 7, 8, 9 })
            {
                var first = new MatchRequest(Profile + seed, 0, 0, 1400, null, null, level);
                var picks = QueueForecast.Ranked(mm, first, 200);
                int depth = mm.Rules.NoSameDeckWithin, relaxed = 0, core = 0;
                for (int i = 0; i < picks.Count; i++)
                {
                    if (NpcDifficultyShaper.IsCoreDeck(picks[i].Encounter.Deck)) core++;
                    if (picks[i].RelaxLevel >= 4) { relaxed++; continue; }   // the deck rule is dropped only when no other deck fits
                    for (int j = Math.Max(0, i - depth); j < i; j++)
                        Program.Require(picks[j].Encounter.Deck.Id != picks[i].Encounter.Deck.Id,
                            level.Id + " star 0 repeats " + picks[i].Encounter.Deck.Id + " within " + depth + " at match " + i);
                }
                Program.Require(core > 100, level.Id + " forecast still reaches the Core step: " + core);
                Program.Require(relaxed < 10, level.Id + " forecast rarely relaxes the deck rule: " + relaxed);
            }
    }

    private static void DisplayCap()
    {
        double legend = LadderRules.RatingForIndex(LadderRules.LegendIndex);
        foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
            foreach (double npc in new[] { 1500.0, legend - 30, legend, legend + 200 })
                Program.Require(level.DisplayRating(npc, LadderRules.LegendIndex - 1, LadderRules.RatingForIndex(LadderRules.LegendIndex - 1)) < legend,
                    level.Id + " never shows a Diamond player a Legend rating (npc " + npc + ")");
        Program.Require(NpcDifficulties.Medium.DisplayRating(legend + 50, LadderRules.LegendIndex, legend + 10) >= legend, "Legend players still see Legend");
    }

    // The REAL client resolver (IsReferencedDeck, not "everything referenced"): a Core-step deck is served only once Begin names it.
    private static void RealResolver()
    {
        var world = Real();
        var mm = new LadderMatchmaker(world);
        try
        {
            NpcWorldRuntime.Install(world, mm);
            NpcWorldRuntime.InstallClientSeams(null);
            foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
            {
                NpcEncounter core = null;
                for (int i = 0; i < 100 && core == null; i++)
                {
                    var e = mm.Choose(new MatchRequest(Profile + 3, i, 0, 1400, null, null, level)).Encounter;
                    if (NpcDifficultyShaper.IsCoreDeck(e.Deck)) core = e;
                }
                Program.Require(core != null, level.Id + " Core encounter exists");
                bool refused = false;
                try { LocalDecks.Get(core.Deck.Id); } catch (InvalidOperationException) { refused = true; }
                Program.Require(refused, "a Core deck does not resolve before Begin");
                NpcWorldRuntime.Begin(core);
                var local = LocalDecks.Get(core.Deck.Id);
                local.Build();
                Program.Require(local.Id == core.Deck.Id, "a Core deck resolves and builds once Begun");
                NpcWorldRuntime.End();
                refused = false;
                try { LocalDecks.Get(core.Deck.Id); } catch (InvalidOperationException) { refused = true; }
                Program.Require(refused, "a Core deck stops resolving when the encounter ends");
            }
            // One NPC_DIFFICULTY line per match, however many matches: a start begins the same encounter twice.
            var lines = new List<string>();
            NpcWorldRuntime.DifficultyLog = lines.Add;
            for (int i = 0; i < 250; i++)
            {
                var e = mm.Choose(new MatchRequest(Profile + 6, i, 5, 1030, null, null, NpcDifficulties.Easy)).Encounter;
                NpcWorldRuntime.Begin(e); NpcWorldRuntime.Begin(e); NpcWorldRuntime.End();
            }
            Program.Require(lines.Count == 250 && lines.All(l => l.StartsWith("NPC_DIFFICULTY level=easy ", StringComparison.Ordinal)),
                "one difficulty line per match: " + lines.Count);
        }
        finally { NpcWorldRuntime.Uninstall(); }
    }

    private static void Casual()
    {
        var world = Real(casual: true);
        var catalog = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json"));
        var mm = new CasualMatchmaker(world, catalog, new NpcLegendField(world));
        for (int i = 0; i < 80; i++)
        {
            var plain = mm.Choose(new CasualRequest(Profile, i, 10, 1300, null));
            var hard = mm.Choose(new CasualRequest(Profile, i, 10, 1300, null, NpcDifficulties.Hard));
            Program.Require(plain.Encounter.Npc.Id == hard.Encounter.Npc.Id && plain.Encounter.Deck.Id == hard.Encounter.Deck.Id
                && ReferenceEquals(plain.Encounter.Skill, hard.Encounter.Skill) && hard.Encounter.Difficulty == null, "Casual Hard unchanged");
            var easy = mm.Choose(new CasualRequest(Profile, i, 10, 1300, null, NpcDifficulties.Easy));
            Program.Require(Math.Abs(easy.Target - (plain.Target - NpcDifficulties.Easy.ShiftRating)) < 1e-9, "Casual Easy target shifted");
            Program.Require(easy.Encounter.Difficulty == "easy" && easy.Encounter.Skill.Id.EndsWith("+easy", StringComparison.Ordinal), "Casual Easy boosted");
            Program.Require(easy.Encounter.DifficultyPosition == NpcDifficulties.Easy.RatingPosition(1300), "no star index: the Casual rating anchors");
        }
        foreach (var level in new[] { NpcDifficulties.Easy, NpcDifficulties.Medium })
        {
            var bronze = new CasualRequest(Profile, 0, 0, LadderRules.StartRating, null, level, 0);
            var picks = QueueForecast.Casual(mm, bronze, 200);
            int core = picks.Count(pick => NpcDifficultyShaper.IsCoreDeck(pick.Encounter.Deck));
            Program.Require(core > 70 && core < 165, level.Id + " Casual mixes Core and themed decks: " + core);
            for (int i = 1; i < picks.Count; i++)
            {
                if (picks[i].RelaxLevel != 0) continue;
                string family = catalog.FamilyOf(picks[i].Encounter.Deck.Id);
                Program.Require(picks.Skip(Math.Max(0, i - mm.Rules.NoSameDeckWithin)).Take(Math.Min(i, mm.Rules.NoSameDeckWithin))
                    .All(previous => catalog.FamilyOf(previous.Encounter.Deck.Id) != family), "Casual repeat rule uses the served deck");
            }
            var high = new CasualRequest(Profile, 200, 0, LadderRules.StartRating + level.ShiftRating, null, level, 0);
            Program.Require(QueueForecast.Casual(mm, high, 40).All(pick => !NpcDifficultyShaper.IsCoreDeck(pick.Encounter.Deck)),
                "Casual wins end the Core step without any Ranked games");
        }
        var both = new CasualRequest(Profile, 0, 0, LadderRules.StartRating + 60, null, NpcDifficulties.Medium, 15);
        Program.Require(both.Position == -15, "Ranked and Casual progress do not count twice");
        var hi = mm.Choose(new CasualRequest(Profile, 3, 10, 1300, null, NpcDifficulties.Medium, 120)).Encounter;
        var lo = mm.Choose(new CasualRequest(Profile, 3, 10, 1300, null, NpcDifficulties.Medium, 20)).Encounter;
        Program.Require(hi.DifficultyPosition == 90 && lo.DifficultyPosition == -10, "Casual Medium position: " + hi.DifficultyPosition + "/" + lo.DifficultyPosition);
    }
}
