using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// WP3 owns this file: personalities, notables, names, cosmetics, deterministic generation, rating spread.
internal static class PopulationSuite
{
    private static readonly Dictionary<string, string> HeroForClass = new Dictionary<string, string> {
        { "WARRIOR", "HERO_01" }, { "SHAMAN", "HERO_02" }, { "ROGUE", "HERO_03" }, { "PALADIN", "HERO_04" }, { "HUNTER", "HERO_05" },
        { "DRUID", "HERO_06" }, { "WARLOCK", "HERO_07" }, { "MAGE", "HERO_08" }, { "PRIEST", "HERO_09" }, { "DEMONHUNTER", "HERO_10" },
        { "DEATHKNIGHT", "HERO_11" } };

    internal static void Run()
    {
        var personalities = PersonalityCatalog.Parse(Program.Data("personalities-v1.json"));
        var notables = NpcNotables.Parse(Program.Data("notables-v1.json"));
        var names = NpcNameTables.Parse(Program.Data("names-v1.json"));
        var cosmetics = NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json"));
        var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        Program.Require(personalities.Archetypes.Count >= 10, "ten personality archetypes");
        Program.Require(notables.All(n => personalities.Find(n.PersonalityId) != null), "notable personalities resolve");
        Program.Require(names.Roots.Count > 0 && cosmetics.CardBacks.Count > 0, "name tables and cosmetics load");

        Personalities(personalities);
        Cosmetics(cosmetics);
        Notables(notables, personalities, skills, cosmetics, names, library);
        Names(names, notables);
        GoldenRoundTrip(notables, personalities, skills, library);
        CatalogSeam();

        NotableDecks(notables, library, SyntheticLibrary(library));
        var real = new NpcWorldCatalogs(library, skills, personalities, notables, names, cosmetics);
        var world = NpcWorld.Generate(real);
        Program.Require(Fingerprint(world) == Fingerprint(NpcWorld.Generate(real)), "two Generate runs are identical");
        Program.Require(Fingerprint(world) != Fingerprint(NpcWorld.Generate(real, NpcWorld.DefaultWorldSeed + 1)), "world seed matters");
        WorldShape(world, real, "real library");
        var legendField = new NpcLegendField(world);
        int playerNumber = legendField.LegendNumber(1860.696, 2), opponentNumber = legendField.LegendNumber(1825, 2);
        Program.Require(legendField.Field(2).Count == 254 && playerNumber == 9 && opponentNumber == 13,
            "preserved Legend match projects player #9 and MarrowMind #13 (actual #" + playerNumber + "/#" + opponentNumber + ")");
        // The field counts NPCs that drift into Legend (rated below it, at most MaxDrift above it). The 100th place stays above
        // that band, so legend:top-100 and legend:1 never depend on them.
        double legendFloor = LadderRules.RatingForIndex(LadderRules.LegendIndex);
        Program.Require(new[] { 0, 500, 2000, 10000 }.All(m => legendField.FieldRating(100, m) > legendFloor + NpcLegendField.MaxDrift),
            "drift into Legend never reaches the top 100");
        Program.Require(world.All.Where(n => !n.Notable && NpcWorld.BandForRating(n.HomeRating) >= LadderBand.Gold).Average(n => n.DeckIds.Count) >= 1.6,
            "a library too small for the cap keeps multi-deck pools");

        var synthetic = new NpcWorldCatalogs(SyntheticLibrary(library), skills, personalities, notables, names, cosmetics);
        var full = NpcWorld.Generate(synthetic);
        Program.Require(Fingerprint(full) == Fingerprint(NpcWorld.Generate(synthetic)), "synthetic world deterministic");
        WorldShape(full, synthetic, "release-size library");
        ArchetypeCap(full);
        Tastes(full, synthetic);
        // The cap binds: one tier-1 deck among seven memes still stays <= 15% of every band pool.
        var skewed = new NpcDeckLibrary(1, 1, "violet-hold", Enumerable.Range(0, 8).Select(i => new NpcDeckEntry("npc-skew-" + i, 1, "Skew " + i,
            i % 2 == 0 ? "MAGE" : "WARRIOR", i % 2 == 0 ? "HERO_08" : "HERO_01", "synthetic", "admitted", null,
            new NpcDeckTags(DeckArchetype.Midrange, i == 0 ? 1 : 5, 3, 0, i == 0 ? 5 : 1, LadderBand.Bronze, LadderBand.Legend),
            null, new[] { new NpcDeckCard("CS2_182", 30) }, null)));
        var skewedWorld = NpcWorld.Generate(new NpcWorldCatalogs(skewed, skills, personalities, new NpcIdentity[0], names, cosmetics));
        var crowd = Enumerable.Range(0, 120).Select(i => new NpcIdentity("npc.n.crowd-" + i, 1, "Crowd" + i, "Test notable.", true, 1700 + i % 50,
            "pro", DeckTaste.Tryhard, new[] { "MAGE" }, new[] { "npc-skew-0" }, null, null, 0, "elite")).ToList();
        var crowded = NpcWorld.Generate(new NpcWorldCatalogs(skewed, skills, personalities, crowd, names, cosmetics));
        var legend = crowded.All.Where(n => NpcWorld.BandForRating(n.HomeRating) == LadderBand.Legend).SelectMany(n => n.DeckIds).ToList();
        Program.Require(legend.GroupBy(id => id).Where(g => g.Key != "npc-skew-0").All(g => g.Count() <= Math.Floor(0.15 * legend.Count) + 1e-9)
            && legend.Count(id => id == "npc-skew-0") > 0.15 * legend.Count, "repair skips a signature-locked deck and still caps the rest");
        ArchetypeCap(skewedWorld);
        Program.Require(skewedWorld.All.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)).All(g => g.SelectMany(n => n.DeckIds)
            .Count(id => id == "npc-skew-0") >= 0.14 * g.SelectMany(n => n.DeckIds).Count()), "the popular deck fills up to the cap");
        Encounters(full, synthetic);
        Console.WriteLine("population: " + Summary(world) + " | release-size library: " + Summary(full));
        // Owner/lead review aid: NPC_WORLD_DUMP=<file.tsv> writes the release-size world (no effect otherwise).
        string dump = Environment.GetEnvironmentVariable("NPC_WORLD_DUMP");
        if (!string.IsNullOrEmpty(dump))
            File.WriteAllLines(dump, new[] { "id\tname\trating\tband\tpersonality\ttaste\tskill\tclasses\tdecks\tskins\tcardBack" }
                .Concat(full.All.Select(n => string.Join("\t", n.Id, n.DisplayName, NpcWorldJson.Invariant(n.HomeRating),
                    NpcWorld.BandForRating(n.HomeRating), n.PersonalityId, n.Taste, n.SkillId, string.Join(",", n.FavoriteClasses),
                    string.Join(",", n.DeckIds), string.Join(",", n.HeroSkins.Values), n.CardBack))));
    }

    private static string Summary(NpcWorld world)
    {
        var bands = world.All.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)).OrderBy(g => g.Key)
            .Select(g => g.Key + "=" + g.Count() + "(cap " + (g.SelectMany(n => n.DeckIds).GroupBy(id => id).Max(x => x.Count())
                / (double)g.SelectMany(n => n.DeckIds).Count()).ToString("P1", CultureInfo.InvariantCulture) + ")");
        return world.All.Count + " NPCs " + string.Join(" ", bands) + " top100-notables="
            + world.All.Take(NpcPopulationRules.EliteBoardSize).Count(n => n.Notable);
    }

    // ---- personalities ----
    private static void Personalities(PersonalityCatalog catalog)
    {
        foreach (string id in new[] { "pro", "veteran", "gambler", "grinder", "showboat", "silent", "friendly", "professor", "speedster", "chaotic" })
            Program.Require(catalog.Find(id) != null, "archetype " + id);
        foreach (var a in catalog.Archetypes)
        {
            Program.Require(!a.Warm || !a.Emotes.Rules.Any(r => r.Mock || r.Emote == "Threaten"), "warm archetype never mocks or Threatens: " + a.Id);
            Program.Require(a.Emotes.Rules.Where(r => r.Mock).All(r => r.Trigger == NpcReaction.HumanMisplay || r.Trigger == NpcReaction.HumanBigTurn),
                "mocks answer public human play only: " + a.Id);
            Program.Require(a.Emotes.Rules.All(r => PersonalityCatalog.BotEmotes.Contains(r.Emote)), "native bot emotes only: " + a.Id);
            Program.Require(a.Emotes.Budget >= 1 && a.Emotes.Budget <= 6, "emote budget 1-6: " + a.Id);
            Program.Require(a.BandWeights.Count == 6 && a.BandWeights.Any(w => w > 0), "band weights: " + a.Id);
        }
        Program.Require(catalog.Archetypes.Count(a => a.Warm) >= 3 && catalog.Archetypes.Count(a => a.CanMock) >= 2, "warm and cocky archetypes exist");
        // Distinct: no two archetypes share pacing, concession and emote budget at once.
        Program.Require(catalog.Archetypes.Select(a => a.PreTurnMinSeconds + "/" + a.ConcessionWillingness + "/" + a.Emotes.Budget + "/" + a.Emotes.Rules.Count)
            .Distinct().Count() == catalog.Archetypes.Count, "archetypes are distinct");
        // Low bands favour chaotic/friendly, high bands silent/pro/grinder.
        Program.Require(catalog.Find("chaotic").BandWeight(LadderBand.Bronze) > catalog.Find("chaotic").BandWeight(LadderBand.Legend)
            && catalog.Find("friendly").BandWeight(LadderBand.Bronze) > catalog.Find("friendly").BandWeight(LadderBand.Legend)
            && new[] { "silent", "pro", "grinder" }.All(id => catalog.Find(id).BandWeight(LadderBand.Legend) > catalog.Find(id).BandWeight(LadderBand.Bronze)),
            "personality band weights follow the spec");

        RequireThrows(() => PersonalityCatalog.Parse(OneArchetype("true", "{\"trigger\":\"HumanMisplay\",\"emote\":\"Oops\",\"chance\":1,\"mock\":true}")),
            "warm archetype with a mock rule fails to load");
        RequireThrows(() => PersonalityCatalog.Parse(OneArchetype("true", "{\"trigger\":\"BotLethalFound\",\"emote\":\"Threaten\",\"chance\":1}")),
            "warm archetype with Threaten fails to load");
        RequireThrows(() => new PersonalityArchetype("w", "W", .1, .2, .1, .2, .5, .5, 8, 0, 0, true,
            new EmoteProfile(2, 10, new EmoteRule(NpcReaction.HumanMisplay, "Oops", 1, mock: true))), "warm mock rejected by the constructor");
        RequireThrows(() => new PersonalityArchetype("w", "W", .1, .2, .1, .2, .5, .5, 8, 0, 0, true,
            new EmoteProfile(2, 10, new EmoteRule(NpcReaction.MatchStart, "Threaten", 1))), "warm Threaten rejected by the constructor");
        foreach (string bad in new[] { "[1,1,1,1,1,\"x\"]", "[1,1,1,1,1,null]", "[1,1,1,1,1,true]", "[1,1,1,1,1,-1]", "[1,1,1]" })
            RequireThrows(() => PersonalityCatalog.Parse(OneArchetype("false", "{\"trigger\":\"MatchStart\",\"emote\":\"Wow\",\"chance\":1}")
                .Replace("\"warm\":false", "\"warm\":false,\"bandWeights\":" + bad)), "bad bandWeights " + bad + " is InvalidDataException");
        RequireThrows(() => PersonalityCatalog.Parse(OneArchetype("false", "{\"trigger\":\"MatchStart\",\"emote\":\"Sorry\",\"chance\":1}")),
            "unqualified bot emote fails to load");
        RequireThrows(() => PersonalityCatalog.Parse(OneArchetype("false", "{\"trigger\":\"MatchStart\",\"emote\":\"Wow\",\"chance\":1,\"mock\":true}")),
            "mock outside public human-play triggers fails to load");
        Program.Require(PersonalityCatalog.Parse(OneArchetype("false", "{\"trigger\":\"HumanMisplay\",\"emote\":\"Oops\",\"chance\":1,\"mock\":true}"))
            .Archetypes[0].CanMock, "cold archetype may mock");
    }

    private static string OneArchetype(string warm, string rule) =>
        "{\"version\":1,\"archetypes\":[{\"id\":\"x\",\"label\":\"X\",\"pacing\":{\"preTurnMin\":0.1,\"preTurnMax\":0.2,\"actionMin\":0.1,\"actionMax\":0.2},"
        + "\"replyBlendBeta\":0.5,\"concessionWillingness\":0.5,\"concedeFromTurn\":8,\"warm\":" + warm
        + ",\"emotes\":{\"budget\":2,\"gapSeconds\":10,\"rules\":[" + rule + "]}}]}";

    // ---- cosmetics ----
    private static void Cosmetics(NpcCosmeticPool pool)
    {
        foreach (string hero in HeroForClass.Values)
        {
            IReadOnlyList<string> skins;
            Program.Require(pool.HeroSkinsByHero.TryGetValue(hero, out skins) && skins.Count >= 5, "five curated skins for " + hero);
            Program.Require(skins.All(s => s.StartsWith(hero, StringComparison.Ordinal)), "skins match their hero: " + hero);
        }
        Program.Require(pool.HeroSkinsByHero.SelectMany(p => p.Value).Distinct().Count() == pool.HeroSkinsByHero.Sum(p => p.Value.Count), "skins unique");
        Program.Require(pool.CardBacks.Count >= 31 && pool.CardBacks.Distinct().Count() == pool.CardBacks.Count && pool.CardBacks.Contains(0),
            "30-40 curated card backs plus the default");
    }

    // ---- notables ----
    private static void Notables(IReadOnlyList<NpcIdentity> notables, PersonalityCatalog personalities, SkillCatalog skills,
        NpcCosmeticPool cosmetics, NpcNameTables names, NpcDeckLibrary library)
    {
        Program.Require(notables.Count >= 40 && notables.Count <= 60, "40-60 notables (" + notables.Count + ")");
        Program.Require(notables.Select(n => n.Id).Distinct().Count() == notables.Count, "notable ids unique");
        Program.Require(notables.Select(n => n.DisplayName.ToLowerInvariant()).Distinct().Count() == notables.Count, "notable names unique");
        Program.Require(Enumerable.Range(1, 3).All(i => notables.Any(n => n.Id == "npc.golden." + i)), "golden ids kept");
        foreach (var n in notables)
        {
            string who = n.Id + " " + n.DisplayName;
            Program.Require(n.Id.StartsWith("npc.golden.", StringComparison.Ordinal) || n.Id.StartsWith("npc.n.", StringComparison.Ordinal), "notable id shape: " + who);
            Program.Require(n.Notable && NpcNameTables.IsWellFormed(n.DisplayName) && !names.IsBlocked(n.DisplayName), "notable name clean: " + who);
            Program.Require(!string.IsNullOrWhiteSpace(n.Bio) && n.Bio.Length <= 140 && n.Bio.IndexOf('\n') < 0, "one-line bio: " + who);
            Program.Require(n.SkillId != null && skills.Find(n.SkillId) != null, "notable skill tier: " + who);
            Program.Require(n.DeckIds.Count >= 2 && n.DeckIds.Count <= 5, "2-5 signature decks: " + who);
            Program.Require(n.DeckIds.All(id => library.Find(id) != null || id.StartsWith("npc-recipe-", StringComparison.Ordinal)
                || id.StartsWith("npc-", StringComparison.Ordinal)), "deck ids are library ids: " + who);
            Program.Require(n.FavoriteClasses.Count >= 1 && n.FavoriteClasses.All(HeroForClass.ContainsKey), "classes: " + who);
            foreach (var skin in n.HeroSkins)
            {
                IReadOnlyList<string> pool;
                Program.Require(n.FavoriteClasses.Any(c => HeroForClass[c] == skin.Key), "skin hero is the notable's class: " + who);
                Program.Require(cosmetics.HeroSkinsByHero.TryGetValue(skin.Key, out pool) && pool.Contains(skin.Value), "skin in curated pool: " + who);
            }
            Program.Require(cosmetics.CardBacks.Contains(n.CardBack), "card back in curated pool: " + who);
            var library_deck = n.DeckIds.Select(library.Find).FirstOrDefault(d => d != null);
            Program.Require(library_deck == null || n.FavoriteClasses.Contains(library_deck.Class), "signature deck fits class: " + who);
        }
        var bands = notables.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)).ToDictionary(g => g.Key, g => g.Count());
        Program.Require(Enumerable.Range(0, 6).All(b => bands.ContainsKey((LadderBand)b) && bands[(LadderBand)b] >= 3), "notables in every band");
        Program.Require(bands[LadderBand.Legend] >= 15, "strong Legend contingent (" + bands[LadderBand.Legend] + ")");
        Program.Require(notables.Select(n => n.PersonalityId).Distinct().Count() == personalities.Archetypes.Count, "every archetype has a notable");
        Program.Require(HeroForClass.Keys.All(c => notables.Count(n => n.FavoriteClasses.Contains(c)) >= 3), "every class has notables");
    }

    // ---- notable deck resolution (real decks-v1 and release-size library: no unresolved ids, no stand-ins) ----
    internal static NotableDeckReport NotableDeckResolution(IReadOnlyList<NpcIdentity> notables, NpcDeckLibrary library) =>
        NotableDeckReport.For(notables, library);

    private static void NotableDecks(IReadOnlyList<NpcIdentity> notables, NpcDeckLibrary library, NpcDeckLibrary releaseSize)
    {
        var report = NotableDeckResolution(notables, library);
        Console.WriteLine("notable decks vs decks-v1: " + report + (report.UnresolvedIds == 0 ? "" : " (missing: "
            + string.Join(",", report.Rows.SelectMany(r => r.Unresolved).Distinct().OrderBy(id => id, StringComparer.Ordinal)) + ")"));
        // Integration: every notable signature deck is an admitted decks-v1 deck (no stand-ins in the shipped world).
        Program.Require(report.UnresolvedIds == 0 && report.StandIns == 0, "real decks-v1 resolves every notable deck: " + report);
        foreach (var row in report.Rows.Where(r => r.StandIn != null))
        {
            var notable = notables.Single(n => n.Id == row.NotableId);
            bool classDeckExists = library.Admitted.Any(d => notable.FavoriteClasses.Contains(d.Class));
            Program.Require(row.OffClass == !classDeckExists, "stand-in is off-class only when no class deck exists: " + row.NotableId);
            Program.Require(row.OffClass || notable.FavoriteClasses.Contains(library.Find(row.StandIn).Class), "class-correct stand-in: " + row.NotableId);
        }
        var logged = new List<string>();
        try
        {
            NpcPopulationRules.Log = logged.Add;
            NpcWorld.Generate(new NpcWorldCatalogs(library, SkillCatalog.Parse(Program.Data("skills-v1.json")),
                PersonalityCatalog.Parse(Program.Data("personalities-v1.json")), notables, NpcNameTables.Parse(Program.Data("names-v1.json")), null));
        }
        finally { NpcPopulationRules.Log = null; }
        Program.Require(logged.Count == report.StandIns && logged.Count(l => l.Contains("OFF-CLASS")) == report.OffClassStandIns, "stand-ins are logged");
        var full = NotableDeckResolution(notables, releaseSize);
        Program.Require(full.UnresolvedIds == 0 && full.StandIns == 0, "release-size library resolves every notable deck: " + full);
    }

    // ---- names ----
    private static void Names(NpcNameTables names, IReadOnlyList<NpcIdentity> notables)
    {
        var taken = new HashSet<string>(notables.Select(n => n.DisplayName), StringComparer.OrdinalIgnoreCase);
        var made = new List<string>();
        for (int i = 0; i < 6000; i++) made.Add(names.Generate(StableSeed.Derive(99, i), taken));
        Program.Require(made.Distinct(StringComparer.OrdinalIgnoreCase).Count() == made.Count, "generated names unique");
        Program.Require(made.All(n => NpcNameTables.IsWellFormed(n) && !names.IsBlocked(n)), "generated names clean and BattleTag-shaped");
        Program.Require(!made.Any(n => notables.Any(x => string.Equals(x.DisplayName, n, StringComparison.OrdinalIgnoreCase))), "never a notable name");
        Program.Require(made.Select(n => n.Length).Distinct().Count() >= 6 && made.Any(n => n.Any(char.IsDigit)) && made.Any(n => !n.Any(char.IsDigit))
            && made.Any(n => n == n.ToLowerInvariant()), "names vary in shape");
        Program.Require(names.Generate(1234, new HashSet<string>()) == names.Generate(1234, new HashSet<string>()), "name generation deterministic");
        var ordinal = new HashSet<string>(StringComparer.Ordinal) { names.Generate(1234, null).ToUpperInvariant() };
        Program.Require(!string.Equals(names.Generate(1234, ordinal), names.Generate(1234, null), StringComparison.OrdinalIgnoreCase),
            "uniqueness is case-insensitive for any set");
        foreach (string bad in new[] { "Hitler88", "xXNaziXx", "Sh1tLord", "Kripp42", "FuckYou" })
            Program.Require(names.IsBlocked(bad), "blocklist catches " + bad);
        Program.Require(!names.IsBlocked("MurlocMain") && !names.IsBlocked("Pebble42"), "blocklist leaves clean names");
        foreach (string code in new[] { "Hawk88", "Owl14", "Yeti1488", "Fox21488", "raven88", "Moth8814" })
            Program.Require(names.IsBlocked(code), "hate-code number blocked: " + code);
        Program.Require(!names.IsBlocked("Hawk1888") && !names.IsBlocked("Owl41") && !names.IsBlocked("Yeti2014"), "other numbers allowed");
        Program.Require(made.All(n => !System.Text.RegularExpressions.Regex.Matches(n, "[0-9]+").Cast<System.Text.RegularExpressions.Match>()
            .Any(m => m.Value == "14" || m.Value == "88" || m.Value.Contains("1488"))), "generated names carry no hate codes");
        var tiny = new NpcNameTables(new string[0], new[] { "Ab" }, new string[0], new[] { "{Root}" }, new string[0]);
        var full = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ab" };
        for (int n = 100; n < 10000; n++) full.Add("Ab" + n.ToString(CultureInfo.InvariantCulture));
        try { tiny.Generate(1, full); throw new InvalidOperationException("FAIL: exhausted name tables must throw"); }
        catch (InvalidOperationException error) when (!error.Message.StartsWith("FAIL", StringComparison.Ordinal)) { }
    }

    // ---- golden round-trip: world notables + NpcEncounterProfiles.Build == NpcCatalog.All ----
    private static void GoldenRoundTrip(IReadOnlyList<NpcIdentity> notables, PersonalityCatalog personalities, SkillCatalog skills, NpcDeckLibrary library)
    {
        foreach (var golden in NpcCatalog.All)
        {
            var notable = notables.Single(n => n.Id == golden.Id);
            var deck = library.Find(golden.DeckId);
            Program.Require(deck != null && notable.DeckIds[0] == deck.Id, "golden deck resolves: " + golden.Id);
            var skill = skills.Find(notable.SkillId);
            var built = NpcEncounterProfiles.Build(notable, deck, personalities.Find(notable.PersonalityId), skill);
            // Elite+ (2026-10-25): elite notables (golden.1) think with the elite+ budget in the world; the off-world
            // NpcCatalog profile keeps the golden budget. Every other tier (golden.3 is master) round-trips exactly, budget included.
            Program.Require(ProfileText(built, false) == ProfileText(golden, false), "golden round-trip " + golden.Id + "\n" + ProfileText(built) + "\nvs\n" + ProfileText(golden));
            Program.Require(BudgetText(built.Budget) == BudgetText(skill.Budget), "notable budget is its skill tier's: " + golden.Id);
            if (skill.Id != "elite") Program.Require(BudgetText(built.Budget) == BudgetText(golden.Budget), "non-elite notable keeps the golden budget: " + golden.Id);
            else Program.Require(built.Budget.TurnSearchSeconds > 0 && built.Budget.TurnWallSeconds > 0, "elite notable runs elite+: " + golden.Id);
        }
    }

    // ---- seam S1 ----
    private static void CatalogSeam()
    {
        var golden = NpcCatalog.All[0];
        try
        {
            NpcWorldHooks.Reset();
            Program.Require(NpcCatalog.ForDeck(golden.DeckId) == golden && NpcCatalog.ForDeck("npc-unknown") == null, "S1 without hook = golden catalog");
            var world = new NpcProfile("npc.g.0001", 1, "Pebble42", "npc-recipe-burn-mage", null, 0, golden.Personality, golden.Budget, "auto:npc-recipe-burn-mage");
            NpcWorldHooks.ProfileForDeck = id => id == world.DeckId ? world : null;
            Program.Require(NpcCatalog.ForDeck(world.DeckId) == world, "S1 hook profile wins");
            Program.Require(NpcCatalog.ForDeck(golden.DeckId) == golden, "S1 hook null falls back to golden");
            Exception reported = null;
            NpcCatalog.ProfileHookFailed = error => reported = error;
            NpcWorldHooks.ProfileForDeck = id => { throw new InvalidOperationException("hook boom"); };
            Program.Require(NpcCatalog.ForDeck(golden.DeckId) == golden && reported?.Message == "hook boom", "S1 throwing hook falls back and reports");
            NpcCatalog.ProfileHookFailed = error => { throw new Exception("logger boom"); };
            Program.Require(NpcCatalog.ForDeck(golden.DeckId) == golden && NpcCatalog.ForDeck("npc-unknown") == null, "S1 survives a throwing logger too");
        }
        finally { NpcWorldHooks.Reset(); NpcCatalog.ProfileHookFailed = null; }
    }

    // ---- world-level acceptance ----
    private static void WorldShape(NpcWorld world, NpcWorldCatalogs catalogs, string label)
    {
        var all = world.All;
        Program.Require(all.Count == catalogs.Notables.Count + NpcWorld.GeneratedCount, label + ": notables + 1500 generated");
        Program.Require(all.Zip(all.Skip(1), (a, b) => a.HomeRating >= b.HomeRating).All(x => x), label + ": sorted by rating");
        Program.Require(all.Select(n => n.Id).Distinct().Count() == all.Count && all.All(n => world.Find(n.Id) == n), label + ": ids unique and indexed");
        foreach (var notable in catalogs.Notables)
        {
            var copy = world.Find(notable.Id);
            Program.Require(copy != null && copy.Notable && copy.DisplayName == notable.DisplayName && copy.HomeRating == notable.HomeRating
                && copy.PersonalityId == notable.PersonalityId && copy.SkillId == notable.SkillId && copy.DeckIds.Count >= 1
                && copy.DeckIds.All(id => catalogs.Decks.Find(id) != null), label + ": notable kept as written " + notable.Id);
        }
        var byBand = all.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)).ToDictionary(g => g.Key, g => g.Count());
        foreach (LadderBand band in Enum.GetValues(typeof(LadderBand)))
            Program.Require(byBand.ContainsKey(band) && byBand[band] >= 150, label + ": >= 150 NPCs in " + band + " (" + (byBand.ContainsKey(band) ? byBand[band] : 0) + ")");
        Program.Require(byBand[LadderBand.Legend] >= 150 && byBand[LadderBand.Legend] <= 300, label + ": 150-300 Legend field (" + byBand[LadderBand.Legend] + ")");
        Program.Require(all.Max(n => n.HomeRating) < NpcPopulationRules.LegendCap && all.Min(n => n.HomeRating) >= 920, label + ": rating range");
        Program.Require(all.GroupBy(n => n.HomeRating).Max(g => g.Count()) <= 4, label + ": no rating pile-up at the cap");
        var top = all.Take(NpcPopulationRules.EliteBoardSize).ToList();
        Program.Require(top.All(n => n.HomeRating >= LadderRules.RatingForIndex(LadderRules.LegendIndex)) && top.Count(n => n.Notable) >= 10,
            label + ": elite board is Legend with notables on it (" + top.Count(n => n.Notable) + ")");

        var generated = all.Where(n => !n.Notable).ToList();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Program.Require(all.All(n => taken.Add(n.DisplayName)), label + ": names unique case-insensitively");
        Program.Require(generated.All(n => NpcNameTables.IsWellFormed(n.DisplayName) && !catalogs.Names.IsBlocked(n.DisplayName)), label + ": names clean");
        Program.Require(generated.All(n => catalogs.Personalities.Find(n.PersonalityId) != null && n.SkillId != null && catalogs.Skills.Find(n.SkillId) != null),
            label + ": personality and skill resolve");
        Program.Require(generated.All(n => n.DeckIds.Count >= 1 && n.DeckIds.Count <= 4 && n.DeckIds.Distinct().Count() == n.DeckIds.Count
            && n.DeckIds.All(id => catalogs.Decks.Find(id)?.Admitted == true)), label + ": 1-3 admitted decks (+1 brewer)");
        Program.Require(generated.All(n => n.DeckIds.Select(catalogs.Decks.Find).Take(1).All(d => d.Tags.Allows(NpcWorld.BandForRating(n.HomeRating)))
            || !catalogs.Decks.ForBand(NpcWorld.BandForRating(n.HomeRating)).Any()), label + ": signature deck fits the home band");
        foreach (var n in generated)
        {
            var heroes = n.DeckIds.Select(id => catalogs.Decks.Find(id).Hero).ToList();
            Program.Require(n.HeroSkins.All(s => heroes.Contains(s.Key) && catalogs.Cosmetics.HeroSkinsByHero[s.Key].Contains(s.Value)),
                label + ": skins match deck heroes " + n.Id);
            Program.Require(catalogs.Cosmetics.CardBacks.Contains(n.CardBack), label + ": card back from pool " + n.Id);
            if (NpcWorld.BandForRating(n.HomeRating) == LadderBand.Legend)
                Program.Require(heroes.Distinct().All(h => n.HeroSkins.ContainsKey(h)) && n.CardBack != 0, label + ": Legend NPC wears a skin " + n.Id);
        }
        var low = generated.Where(n => NpcWorld.BandForRating(n.HomeRating) != LadderBand.Legend).ToList();
        double baseShare = low.Count(n => n.HeroSkins.Count == 0) / (double)low.Count;
        Program.Require(baseShare > 0.33 && baseShare < 0.47, label + ": ~40% base hero below Legend (" + baseShare.ToString("F3", CultureInfo.InvariantCulture) + ")");
        // Skill rises with rating.
        var meanTier = Enum.GetValues(typeof(LadderBand)).Cast<LadderBand>().Select(b => generated
            .Where(n => NpcWorld.BandForRating(n.HomeRating) == b).Average(n => catalogs.Skills.Find(n.SkillId).Tier)).ToList();
        Program.Require(meanTier.Zip(meanTier.Skip(1), (a, b) => a < b).All(x => x), label + ": mean skill tier rises by band");
        Program.Require(catalogs.Personalities.Archetypes.All(a => generated.Count(n => n.PersonalityId == a.Id) >= 30), label + ": every archetype populated");
    }

    // Archetype cap: no library deck above 15% of any band's deck pool (notables included).
    private static void ArchetypeCap(NpcWorld world)
    {
        foreach (var band in world.All.GroupBy(n => NpcWorld.BandForRating(n.HomeRating)))
        {
            var entries = band.SelectMany(n => n.DeckIds).ToList();
            var worst = entries.GroupBy(id => id).OrderByDescending(g => g.Count()).First();
            double share = worst.Count() / (double)entries.Count;
            Program.Require(share <= NpcPopulationRules.ArchetypeCap + 1e-9,
                "archetype share <= 15% in " + band.Key + ": " + worst.Key + " " + share.ToString("F3", CultureInfo.InvariantCulture));
        }
    }

    private static void Tastes(NpcWorld world, NpcWorldCatalogs catalogs)
    {
        var generated = world.All.Where(n => !n.Notable).ToList();
        Func<LadderBand, Func<NpcIdentity, bool>, double> share = (band, predicate) => {
            var rows = generated.Where(n => NpcWorld.BandForRating(n.HomeRating) == band).ToList();
            return rows.Count(predicate) / (double)rows.Count;
        };
        Program.Require(share(LadderBand.Legend, n => n.Taste == DeckTaste.Tryhard) > share(LadderBand.Bronze, n => n.Taste == DeckTaste.Tryhard),
            "tryhards rise with rating");
        Program.Require(share(LadderBand.Bronze, n => n.Taste == DeckTaste.MemeLord) > share(LadderBand.Legend, n => n.Taste == DeckTaste.MemeLord),
            "meme lords common low");
        Program.Require(share(LadderBand.Bronze, n => n.PersonalityId == "chaotic" || n.PersonalityId == "friendly")
            > share(LadderBand.Legend, n => n.PersonalityId == "chaotic" || n.PersonalityId == "friendly"), "chaotic/friendly common low");
        Program.Require(share(LadderBand.Legend, n => n.PersonalityId == "silent" || n.PersonalityId == "pro" || n.PersonalityId == "grinder")
            > share(LadderBand.Bronze, n => n.PersonalityId == "silent" || n.PersonalityId == "pro" || n.PersonalityId == "grinder"), "silent/pro/grinder common high");
        Program.Require(generated.Where(n => n.Taste == DeckTaste.ClassLoyalist).All(n => n.FavoriteClasses.Count <= 2
            && n.DeckIds.All(id => n.FavoriteClasses.Contains(catalogs.Decks.Find(id).Class))), "class loyalists stay in 1-2 classes");
        var brewers = generated.Where(n => n.Taste == DeckTaste.Brewer).ToList();
        Program.Require(brewers.Count(n => n.DeckIds.Any(id => !catalogs.Decks.Find(id).Tags.Allows(NpcWorld.BandForRating(n.HomeRating))))
            >= brewers.Count * 0.8, "brewers carry an off-band deck");
        Func<IEnumerable<NpcIdentity>, double> power = rows => rows.SelectMany(n => n.DeckIds).Average(id => catalogs.Decks.Find(id).Tags.PowerTier);
        Program.Require(power(generated.Where(n => n.Taste == DeckTaste.Tryhard)) < power(generated.Where(n => n.Taste == DeckTaste.MemeLord)),
            "tryhards play stronger decks than meme lords");
        var warm = catalogs.Personalities.Archetypes.Where(a => a.Warm).Select(a => a.Id).ToList();
        Program.Require(generated.Where(n => warm.Contains(n.PersonalityId)).All(n => !catalogs.Personalities.Find(n.PersonalityId).CanMock),
            "warm NPCs cannot mock");
    }

    // Every NPC/deck pair builds a playable profile.
    private static void Encounters(NpcWorld world, NpcWorldCatalogs catalogs)
    {
        foreach (var npc in world.All)
            foreach (string id in npc.DeckIds)
            {
                var deck = catalogs.Decks.Find(id);
                var profile = NpcEncounterProfiles.Build(npc, deck, catalogs.Personalities.Find(npc.PersonalityId), catalogs.Skills.Find(npc.SkillId));
                Program.Require(profile.Id == npc.Id && profile.DeckId == id && profile.DisplayName == npc.DisplayName
                    && profile.PlanId == deck.EffectivePlanId && profile.Budget != null && profile.Personality.Emotes != null
                    && (profile.HeroSkin == null || profile.HeroSkin.StartsWith(deck.Hero, StringComparison.Ordinal)), "encounter profile " + npc.Id + "/" + id);
            }
    }

    // ---- helpers ----
    private static string Fingerprint(NpcWorld world)
    {
        var text = new StringBuilder();
        foreach (var n in world.All)
            text.Append(n.Id).Append('|').Append(n.DisplayName).Append('|').Append(NpcWorldJson.Invariant(n.HomeRating)).Append('|')
                .Append(n.PersonalityId).Append('|').Append(n.Taste).Append('|').Append(n.SkillId).Append('|')
                .Append(string.Join(",", n.FavoriteClasses)).Append('|').Append(string.Join(",", n.DeckIds)).Append('|')
                .Append(string.Join(",", n.HeroSkins.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value))).Append('|')
                .Append(string.Join(",", n.HeroSkinPowers.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value))).Append('|')
                .Append(n.CardBack).Append('\n');
        return text.ToString();
    }

    private static string BudgetText(Budget b) => string.Join("|", b.TurnNodes, b.PlanNodes, b.LethalNodes, b.ReplyK, b.ReplyL, b.ReplyR,
        b.OwnBeamWidth, b.Dedupe, N(b.TurnSearchSeconds), N(b.TurnWallSeconds));
    private static string ProfileText(NpcProfile p, bool budget = true)
    {
        var x = p.Personality; var e = x.Emotes;
        return string.Join("|", p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin ?? "", p.HeroSkinPower ?? "", p.CardBack, p.PlanId,
            budget ? BudgetText(p.Budget) : "-",
            N(x.PreTurnMinSeconds), N(x.PreTurnMaxSeconds), N(x.ActionMinSeconds), N(x.ActionMaxSeconds), N(x.ReplyBlendBeta),
            N(x.ConcessionWillingness), x.ConcedeFromTurn, e.Budget, N(e.MinGapMilliseconds), e.SwingThreshold,
            string.Join(";", e.Rules.Select(r => r.Trigger + "," + r.Emote + "," + N(r.Chance) + "," + r.MaxUses + "," + r.HumanEmote + "," + r.Mock)));
    }

    private static string N(double value) => NpcWorldJson.Invariant(value);

    private static void RequireThrows(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("FAIL: " + message);
    }

    // A release-size library (golden + recipe-derived + batch-3 candidate ids) so the cap and taste rules are exercised
    // before WP1's decks-v1 lands. Tags are WP3 assumptions; WP1's import is authoritative.
    private static NpcDeckLibrary SyntheticLibrary(NpcDeckLibrary real)
    {
        var rows = new[] {
            // recipe-derived (data/shop/recipes-v1.json): strong
            "npc-recipe-egg-death-knight DEATHKNIGHT combo 2 4 1 gold legend", "npc-recipe-void-demon-hunter DEMONHUNTER tempo 2 3 0 gold legend",
            "npc-recipe-attack-druid DRUID aggro 2 3 1 silver legend", "npc-recipe-tripwire-hunter HUNTER aggro 2 2 1 silver legend",
            "npc-recipe-burn-mage MAGE aggro 2 3 2 gold legend", "npc-recipe-equilibrium-quest-priest PRIEST control 2 3 1 gold legend",
            "npc-recipe-aya-coin-rogue ROGUE combo 2 3 1 gold legend", "npc-recipe-mug-shaman SHAMAN midrange 2 3 0 silver legend",
            "npc-recipe-herald-warlock WARLOCK midrange 2 3 1 silver legend",
            // recipe-derived: fun
            "npc-recipe-herald-death-knight DEATHKNIGHT midrange 3 4 0 bronze diamond", "npc-recipe-deathrattle-herald-demon-hunter DEMONHUNTER midrange 3 4 0 bronze platinum",
            "npc-recipe-merithra-druid DRUID control 4 4 0 bronze diamond", "npc-recipe-dragon-hunter HUNTER midrange 3 4 0 bronze diamond",
            "npc-recipe-manastorm-mage MAGE combo 4 5 1 bronze platinum", "npc-recipe-infinity-paladin PALADIN combo 3 4 1 bronze diamond",
            "npc-recipe-medivh-imbue-priest PRIEST control 4 4 1 bronze platinum", "npc-recipe-sneaky-herald-rogue ROGUE tempo 3 4 0 bronze diamond",
            "npc-recipe-zee-herald-shaman SHAMAN tempo 3 4 0 bronze diamond", "npc-recipe-egg-warlock WARLOCK combo 4 5 1 bronze platinum",
            "npc-recipe-cannon-pirate-warrior WARRIOR aggro 3 4 1 bronze diamond",
            // feat/npc-decks-b3 candidates-batch-3 ids
            "npc-death-knight-aggro DEATHKNIGHT aggro 3 2 1 gold diamond", "npc-death-knight-thalena DEATHKNIGHT midrange 2 3 1 gold legend",
            "npc-death-knight-bwonsamdi DEATHKNIGHT midrange 3 5 1 silver platinum", "npc-demon-hunter-raza DEMONHUNTER tempo 1 2 2 platinum legend",
            "npc-demon-hunter-quest DEMONHUNTER combo 3 4 0 silver diamond", "npc-druid-azshara DRUID control 2 3 1 gold legend",
            "npc-hunter-quest HUNTER combo 3 4 0 bronze platinum", "npc-hunter-magmaw HUNTER midrange 4 5 0 bronze gold",
            "npc-hunter-face HUNTER aggro 3 2 1 bronze silver", "npc-mage-leyline MAGE tempo 2 3 1 gold legend",
            "npc-mage-quest MAGE combo 3 4 0 silver diamond", "npc-mage-imbue MAGE midrange 4 5 0 bronze gold",
            "npc-paladin-egg PALADIN combo 3 4 0 silver platinum", "npc-priest-thief PRIEST control 1 3 3 diamond legend",
            "npc-priest-egg PRIEST combo 3 4 1 gold platinum", "npc-priest-reborn PRIEST control 2 3 1 platinum legend",
            "npc-rogue-burn ROGUE tempo 2 3 1 platinum legend", "npc-rogue-mill ROGUE control 3 2 3 platinum diamond",
            "npc-rogue-quest ROGUE combo 4 4 0 bronze gold", "npc-shaman-masochist SHAMAN midrange 3 5 1 silver diamond",
            "npc-shaman-herald SHAMAN midrange 3 3 1 gold platinum", "npc-warlock-pig WARLOCK midrange 2 4 2 platinum legend",
            "npc-warlock-godfrey WARLOCK combo 3 4 2 silver platinum", "npc-warlock-wallow WARLOCK midrange 4 5 1 bronze gold",
            "npc-warrior-egg WARRIOR combo 3 4 1 silver platinum", "npc-warrior-logosh WARRIOR midrange 2 4 1 gold diamond",
            "npc-warrior-quest WARRIOR control 4 5 1 bronze gold" };
        // WP1's decks-v1 is authoritative once it lands: synthesize only ids the real library lacks or has not admitted
        // (a synthetic stand-in replaces a pending/rejected real entry in this synthetic library only).
        var synthetic = rows.Where(r => real.Find(r.Split(' ')[0])?.Admitted != true).ToArray();
        var replaced = new HashSet<string>(synthetic.Select(r => r.Split(' ')[0]));
        var decks = real.Decks.Where(d => !replaced.Contains(d.Id)).ToList();
        foreach (string row in synthetic)
        {
            var f = row.Split(' ');
            decks.Add(new NpcDeckEntry(f[0], 1, f[0], f[1], HeroForClass[f[1]], "synthetic", "admitted", null,
                new NpcDeckTags(NpcDeckLibrary.ParseEnum<DeckArchetype>(f[2]), int.Parse(f[3], CultureInfo.InvariantCulture),
                    3, int.Parse(f[5], CultureInfo.InvariantCulture), int.Parse(f[4], CultureInfo.InvariantCulture),
                    NpcDeckLibrary.ParseEnum<LadderBand>(f[6]), NpcDeckLibrary.ParseEnum<LadderBand>(f[7])),
                null, new[] { new NpcDeckCard("CS2_182", 30) }, null));
        }
        return new NpcDeckLibrary(real.Schema, real.LibraryVersion, real.ActiveMeta, decks);
    }
}
