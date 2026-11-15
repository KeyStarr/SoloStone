using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Enums;
using EngineCard = SabberStoneCore.Model.Card;
using EngineCards = SabberStoneCore.Model.Cards;

// Run modes (plugin/Runs): the shipped definitions (every id real, boss Hero Powers match their heroes, start decks legal,
// coverage report of what blocks the rest), and the run rules on a fixture whose every entry is playable.
internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        checks++;
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { checks++; return; }
        throw new InvalidOperationException("FAIL (no " + typeof(T).Name + "): " + message);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray()));

    private static EngineCard Find(string id)
    {
        try { return EngineCards.FromId(id); }
        catch (KeyNotFoundException) { return null; }
    }

    private static bool Playable(string id) { EngineCard card = Find(id); return card != null && card.Implemented; }
    private static bool Labelled(string id) => Find(id) != null;

    private static int Main(string[] args)
    {
        NpcWorldJson.Parser = text => MiniJSON.Json.Deserialize(text);
        ShippedDefinitions();
        FixtureRules();
        SaveDataProjection();
        ParserRejects();
        Console.WriteLine("PASS runs (" + checks + " checks)");
        return 0;
    }

    private static void ShippedDefinitions()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "data", "runs");
        var files = Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
        Check(files.Count > 0, "data/runs has run definitions");
        foreach (string file in files)
        {
            RunDefinition definition = RunDefinition.Parse(File.ReadAllText(file));
            Check(Path.GetFileName(file) == definition.Id + "-v" + definition.Version + ".json", file + ": file name is <id>-v<version>.json");

            var ids = definition.Classes.SelectMany(c => c.Deck)
                .Concat(definition.Bosses.SelectMany(b => b.AllCards()))
                .Concat(definition.Buckets.SelectMany(b => b.Cards.Concat(new[] { b.Title })))
                .Concat(definition.PassiveTreasures).Concat(definition.ActiveTreasures).Distinct().ToList();
            var unknown = ids.Where(id => Find(id) == null).ToList();
            Check(unknown.Count == 0, definition.Id + ": unknown card ids " + string.Join(", ", unknown));

            foreach (RunClassStart start in definition.Classes)
            {
                CardClass cls = (CardClass)Enum.Parse(typeof(CardClass), start.Class);
                var illegal = start.Deck.Where(id => Find(id).Class != cls && Find(id).Class != CardClass.NEUTRAL).ToList();
                Check(illegal.Count == 0, definition.Id + " " + start.Class + ": start deck has other-class cards " + string.Join(", ", illegal));
                Check(start.Deck.Distinct().Count() == start.Deck.Count, definition.Id + " " + start.Class + ": start deck has one copy of each card");
            }
            foreach (RunBoss boss in definition.Bosses)
            {
                EngineCard hero = Find(boss.Hero);
                Check(hero.Type == CardType.HERO, boss.Id + ": " + boss.Hero + " is a hero card");
                EngineCard power = EngineCards.FromAssetId(hero[GameTag.HERO_POWER]);
                Check(power != null && power.Id == boss.HeroPower, boss.Id + ": heroPower " + boss.HeroPower + " is the hero's own (" + power?.Id + ")");
                Check(boss.Source.StartsWith("https://hearthstone.wiki.gg/wiki/", StringComparison.Ordinal), boss.Id + ": names its wiki source");
            }
            foreach (RunBucket bucket in definition.Buckets)
                Check(bucket.Source.StartsWith("https://hearthstone.wiki.gg/wiki/", StringComparison.Ordinal), bucket.Id + ": names its wiki source");

            RunAdmission admission = RunAdmission.Evaluate(definition, Playable, Labelled);
            Check(admission.IsReady == (admission.Problems().Count == 0), definition.Id + ": readiness agrees with its problems");
            foreach (string line in admission.Report()) Console.WriteLine(line);
            if (!admission.IsReady)
                Throws<InvalidOperationException>(() => new RunEngine(admission).Start(definition.Classes[0].Class, "HERO_08", 1),
                    definition.Id + ": a run that is not ready cannot start");
        }

        RunDefinition loot = RunDefinition.Parse(Read("data", "runs", "loot-v1.json"));
        Check(loot.Client.Scenario == 2663 && loot.Client.ServerKey == 24 && loot.Client.ClientKey == 42, "loot: client ids");
        Check(loot.Classes.Count == 9 && loot.Classes.All(c => c.Deck.Count == 10), "loot: nine classes with ten-card start decks");
        Check(loot.Rounds.Count == 8 && loot.Round(8).BossStartingMana == 2, "loot: eight rounds, the final boss starts with 2 mana");

        // Developer builds play an incomplete run: each round without an admitted boss fights an admitted one, and offers
        // with nothing admitted are skipped. Public builds refuse (checked above for every definition that is not ready).
        RunAdmission lootAdmission = RunAdmission.Evaluate(loot, Playable, Labelled);
        if (!lootAdmission.IsReady && lootAdmission.Bosses.Count > 0)
        {
            var dev = new RunEngine(lootAdmission, allowIncomplete: true);
            RunState devRun = dev.Start("MAGE", "HERO_08", 1);
            for (int round = 1; round <= 3; round++)
            {
                Check(lootAdmission.Bosses.Contains(devRun.NextBoss) && devRun.Round == round, "dev run round " + round + ": an admitted boss");
                dev.ResolveFight(devRun, RunFightResult.Won);
                while (devRun.Offer != null) dev.Choose(devRun, 0);
            }
            Check(devRun.Status == RunStatus.Active && devRun.BossesDefeated == 3, "an incomplete run keeps going in developer builds");
        }
    }

    private static string Describe(RunState s) =>
        string.Join("|", s.Status, s.Step, s.Round, s.NextBoss, s.LostTo, string.Join(",", s.Deck), string.Join(",", s.Passives),
            string.Join(",", s.BossesFought), string.Join(",", s.PendingRewards),
            s.Offer == null ? "-" : s.Offer.Kind + ":" + string.Join(";", s.Offer.Options.Select(o => o.Title + "=" + string.Join(",", o.Cards))));

    // Plays a whole run, always winning and taking the given option; returns the state after each transition.
    private static List<string> PlayToWin(RunEngine engine, long seed, int pick)
    {
        var trace = new List<string>();
        RunState state = engine.Start("MAGE", "HERO_08", seed);
        trace.Add(Describe(state));
        while (state.Status == RunStatus.Active)
        {
            if (state.Offer != null) engine.Choose(state, Math.Min(pick, state.Offer.Options.Count - 1));
            else engine.ResolveFight(state, RunFightResult.Won);
            trace.Add(Describe(state));
        }
        return trace;
    }

    private static void FixtureRules()
    {
        RunDefinition definition = RunDefinition.Parse(Read("fixtures", "complete-run.json"));
        RunAdmission admission = RunAdmission.Evaluate(definition, Playable, Labelled);
        Check(admission.IsReady, "fixture is ready: " + string.Join("; ", admission.Report()));
        var engine = new RunEngine(admission);

        // A whole won run: health, boss rounds, final-boss mana, hero power upgrade, the reward schedule, deck growth.
        RunState state = engine.Start("MAGE", "HERO_08", 42);
        Check(state.Deck.SequenceEqual(definition.ClassStart("MAGE").Deck), "a run starts with the class start deck");
        var offered = new List<string>();
        for (int round = 1; round <= 8; round++)
        {
            RunEncounter encounter = engine.NextEncounter(state);
            Check(encounter.Round == round && encounter.PlayerHealth == 15 + 5 * (round - 1), "round " + round + ": player health " + encounter.PlayerHealth);
            Check(encounter.Boss.Rounds.Contains(round), "round " + round + ": the boss belongs to the round");
            Check(encounter.BossStartingMana == (round == 8 ? 2 : 0), "round " + round + ": boss starting mana");
            EncounterSpec spec = encounter.ToSpec(definition);
            Check(spec.PlayerHero == "HERO_08" && spec.PlayerDeck.SequenceEqual(state.Deck) && spec.PlayerStartingHealth == encounter.PlayerHealth
                && (spec.PlayerLoadout ?? new string[0]).SequenceEqual(state.Passives) && spec.NpcHero == encounter.Boss.Hero
                && spec.NpcDeck.SequenceEqual(encounter.Boss.Deck) && spec.NpcStartingMana == encounter.BossStartingMana
                && !spec.NpcCoin && spec.FirstPlayer == 1 && spec.MissionId == 2663 && spec.BoardId == 61,
                "round " + round + ": the fight is staged from the run (deck, Health, passives, boss, fight rules, mission, board)");
            engine.ResolveFight(state, RunFightResult.Won);
            var kinds = new List<string>();
            while (state.Offer != null)
            {
                RunOffer offer = state.Offer;
                kinds.Add(offer.Kind.ToString());
                Check(offer.Options.Count == RunAdmission.OfferSize, "round " + round + ": an offer shows three options");
                if (offer.Kind == RunRewardKind.Loot)
                {
                    Check(offer.Options.Select(o => o.Title).Distinct().Count() == 3, "loot: three different buckets");
                    foreach (RunOption option in offer.Options)
                    {
                        RunBucket bucket = definition.Buckets.Single(b => b.Title == option.Title);
                        Check(bucket.Class == "MAGE" && option.Cards.Count == 3 && option.Cards.All(bucket.Cards.Contains),
                            "loot: a bundle is three cards of one class bucket");
                    }
                }
                else
                {
                    Check(offer.Options.All(o => o.Title == null && o.Cards.Count == 1), "treasure: one card, no title");
                    Check(offer.Options.Select(o => o.Cards[0]).Distinct().Count() == 3, "treasure: three different treasures");
                    var owned = offer.Kind == RunRewardKind.PassiveTreasure ? state.Passives : state.Deck;
                    Check(offer.Options.All(o => !owned.Contains(o.Cards[0])), "treasure: never one the run already has");
                }
                Throws<InvalidOperationException>(() => engine.ResolveFight(state, RunFightResult.Won), "no fight while a reward is offered");
                Throws<InvalidOperationException>(() => engine.NextEncounter(state), "no encounter while a reward is offered");
                Throws<ArgumentOutOfRangeException>(() => engine.Choose(state, 3), "a choice outside the three options");
                engine.Choose(state, 0);
            }
            offered.Add(round + ":" + string.Join(",", kinds));
        }
        Check(string.Join(" ", offered) == "1:PassiveTreasure,Loot 2:Loot 3:ActiveTreasure,Loot 4:Loot 5:PassiveTreasure,Loot 6:Loot 7:ActiveTreasure,Loot 8:",
            "the reward schedule follows the rounds: " + string.Join(" ", offered));
        Check(state.Status == RunStatus.Won && state.NextBoss == null && state.BossesDefeated == 8, "eight wins win the run");
        Check(state.Deck.Count == 10 + 7 * 3 + 2 && state.Passives.Count == 2, "the deck grew by 7 bundles and 2 treasures: " + state.Deck.Count);
        Check(state.BossesFought.Distinct().Count() == 8, "no boss is fought twice in a won run");
        Throws<InvalidOperationException>(() => engine.ResolveFight(state, RunFightResult.Won), "a finished run takes no fight");
        Throws<InvalidOperationException>(() => engine.Choose(state, 0), "a finished run takes no choice");
        engine.MarkRunEndSeen(state);
        Check(state.RunEndSeen, "the run-end screen is remembered");

        // Same seed and choices: the same run; another seed: another run.
        Check(PlayToWin(engine, 7, 1).SequenceEqual(PlayToWin(engine, 7, 1)), "a run replays exactly from its seed");
        Check(!PlayToWin(engine, 7, 1).SequenceEqual(PlayToWin(engine, 8, 1)), "another seed gives another run");
        Check(PlayToWin(engine, 7, 0).Last() != PlayToWin(engine, 7, 2).Last(), "choices change the run");

        // Forms of one boss (a family) never meet in one run, and both forms do get fought across runs.
        var seenForms = new HashSet<string>();
        for (long seed = 1; seed <= 40; seed++)
        {
            RunState run = engine.Start("MAGE", "HERO_08", seed);
            while (run.Status == RunStatus.Active)
            {
                if (run.Offer != null) engine.Choose(run, 0); else engine.ResolveFight(run, RunFightResult.Won);
            }
            var families = run.BossesFought.Select(id => definition.Boss(id).Family).ToList();
            Check(families.Distinct().Count() == families.Count, "seed " + seed + ": one boss per family: " + string.Join(",", run.BossesFought));
            seenForms.UnionWith(run.BossesFought.Where(id => definition.Boss(id).Family == "priest"));
        }
        Check(seenForms.SetEquals(new[] { "priest-a", "priest-b" }), "both forms of the priest family appear across runs");

        // A loss ends the run on that boss.
        RunState lost = engine.Start("WARRIOR", "HERO_01", 3);
        string boss = lost.NextBoss;
        engine.ResolveFight(lost, RunFightResult.Lost);
        Check(lost.Status == RunStatus.Lost && lost.LostTo == boss && lost.NextBoss == null && lost.Offer == null, "a loss ends the run");
        Throws<InvalidOperationException>(() => engine.NextEncounter(lost), "a lost run has no next fight");
        Throws<InvalidOperationException>(() => engine.MarkRunEndSeen(engine.Start("MAGE", "HERO_08", 3)), "an active run has no run-end screen");

        // A tie faces another boss of the same round; an interrupted fight faces the same boss again, drawing nothing.
        RunState tied = engine.Start("MAGE", "HERO_08", 5);
        string first = tied.NextBoss;
        engine.ResolveFight(tied, RunFightResult.Tied);
        Check(tied.Round == 1 && tied.NextBoss != first && definition.Boss(tied.NextBoss).Rounds.Contains(1)
            && engine.NextEncounter(tied).PlayerHealth == 15, "a tie replays round 1 against its other boss");
        engine.ResolveFight(tied, RunFightResult.Tied);
        Check(tied.Round == 1 && tied.NextBoss == first, "a second tie, every round-1 boss fought: back to the first");
        string before = Describe(tied);
        engine.ResolveFight(tied, RunFightResult.Interrupted);
        Check(Describe(tied) == before, "an interrupted fight changes nothing: same boss, same health, same deck");

        RunState retired = engine.Start("MAGE", "HERO_08", 9);
        engine.Retire(retired);
        Check(retired.Status == RunStatus.Retired && retired.NextBoss == null, "a retired run is over");
        RunState copy = state.Clone();
        Check(Describe(copy) == Describe(state) && !ReferenceEquals(copy.Deck, state.Deck), "Clone copies every list");

        Throws<ArgumentException>(() => engine.Start("PRIEST", "HERO_09", 1), "a class the run does not offer");
        Throws<ArgumentNullException>(() => engine.ResolveFight(null, RunFightResult.Won), "no state");
        var otherVersion = engine.Start("MAGE", "HERO_08", 1);
        otherVersion.DefinitionVersion = 2;
        Throws<InvalidOperationException>(() => engine.ResolveFight(otherVersion, RunFightResult.Won), "a run saved by another definition version");
    }

    // The run as the client's Dungeon Crawl display reads it, and the client's writes as run operations.
    private static void SaveDataProjection()
    {
        RunDefinition definition = RunDefinition.Parse(Read("fixtures", "complete-run.json"));
        var engine = new RunEngine(RunAdmission.Evaluate(definition, Playable, Labelled));
        int Dbf(string id) => EngineCards.FromId(id).AssetId;
        int ClassId(string cls) => (int)Enum.Parse(typeof(CardClass), cls);
        int Health(string id) => EngineCards.FromId(id)[GameTag.HEALTH];
        IReadOnlyDictionary<int, long[]> Project(RunState state, params RunClassWinCount[] wins) =>
            RunSaveData.Project(definition, state, wins, state == null ? 0 : engine.PlayerHealth(state), Dbf, ClassId, Health);
        long One(IReadOnlyDictionary<int, long[]> values, int subkey) => values[subkey].Single();

        var none = Project(null, new RunClassWinCount { Class = "MAGE", Bosses = 8, Runs = 1 });
        Check(One(none, RunSaveData.IsRunActive) == 0 && One(none, RunSaveData.ChosenLoot) == 0 && none[RunSaveData.DeckCardList].Length == 0
            && One(none, 98) == 8 && One(none, 99) == 1 && none[84].Length == 0, "no run: inactive, empty deck, the class crowns");
        Check(RunSaveData.Owned().All(none.ContainsKey), "every owned subkey is sent, empty when unused (so stale values clear)");

        RunState run = engine.Start("MAGE", "HERO_08", 42);
        var start = Project(run);
        RunBoss boss = definition.Boss(run.NextBoss);
        Check(One(start, RunSaveData.IsRunActive) == 1 && One(start, RunSaveData.DeckClass) == (int)CardClass.MAGE
            && One(start, RunSaveData.HeroCardDbId) == Dbf("HERO_08") && One(start, RunSaveData.ScenarioId) == 2663,
            "an active run: class, hero and mission");
        Check(start[RunSaveData.DeckCardList].SequenceEqual(run.Deck.Select(id => (long)Dbf(id))) && One(start, RunSaveData.HeroHealth) == 15,
            "the run deck and the player's Health");
        Check(One(start, RunSaveData.NextBossFight) == Dbf(boss.Hero) && One(start, RunSaveData.NextBossHealth) == Health(boss.Hero)
            && start[RunSaveData.LootOptionA].Length == 0 && start[RunSaveData.TreasureOption].Length == 0, "the next boss, no offer");

        engine.ResolveFight(run, RunFightResult.Won);
        var treasure = Project(run);
        Check(treasure[RunSaveData.TreasureOption].Length == 3 && treasure[RunSaveData.NextBossFight].Length == 0
            && treasure[RunSaveData.BossesDefeated].Single() == Dbf(boss.Hero) && One(treasure, RunSaveData.HeroHealth) == 20,
            "after a win: the treasure offer, the defeated boss, no next boss yet, more Health");
        engine.Choose(run, 0);
        var loot = Project(run);
        RunOption first = run.Offer.Options[0];
        Check(loot[RunSaveData.LootOptionA].SequenceEqual(new long[] { Dbf(first.Title) }.Concat(first.Cards.Select(id => (long)Dbf(id))))
            && loot[RunSaveData.LootOptionC].Length == 4 && loot[RunSaveData.TreasureOption].Length == 0, "loot bundles: [title card, cards]");
        engine.Choose(run, 2);
        engine.ResolveFight(run, RunFightResult.Lost);
        var lost = Project(run);
        Check(One(lost, RunSaveData.IsRunActive) == 0 && One(lost, RunSaveData.BossLostTo) == Dbf(definition.Boss(run.LostTo).Hero)
            && One(lost, RunSaveData.SeenRunComplete) == 0 && lost[RunSaveData.HeroHealth].Length == 0, "a lost run: the boss it lost to, end screen unseen");
        engine.MarkRunEndSeen(run);
        var after = Project(run);
        Check(One(after, RunSaveData.IsRunActive) == 0 && after[RunSaveData.DeckCardList].Length == 0 && after[RunSaveData.BossLostTo].Length == 0,
            "once its end screen was seen, an ended run shows as no run (the display starts over)");

        RunWrite Write(int subkey, params long[] values) => RunSaveData.Interpret(subkey, values);
        Check(Write(101, 2).Kind == RunWriteKind.ChooseLoot && Write(101, 2).Index == 1, "the client's loot choice is 1-based");
        Check(Write(102, 1).Kind == RunWriteKind.ChooseTreasure && Write(102, 1).Index == 0, "the client's treasure choice");
        Check(Write(168, 1).Kind == RunWriteKind.RunEndSeen && Write(662, 1).Kind == RunWriteKind.Retire, "run-end seen and retire");
        Check(Write(101, 0).Kind == RunWriteKind.Ignore && Write(236, 5).Kind == RunWriteKind.Ignore, "anything else is not a run operation");
    }

    private static void ParserRejects()
    {
        string fixture = Read("fixtures", "complete-run.json");
        void Rejects(string from, string to, string why)
        {
            Check(fixture.Contains(from), "parser case applies: " + why);
            Throws<InvalidDataException>(() => RunDefinition.Parse(fixture.Replace(from, to)), why);
        }
        Rejects("\"solostone.run/1\"", "\"solostone.run/2\"", "an unknown schema");
        Rejects("{ \"round\": 2,", "{ \"round\": 3,", "rounds out of order");
        Rejects("{ \"round\": 8, \"rewards\": []", "{ \"round\": 8, \"rewards\": [\"loot\"]", "the last round offers rewards");
        Rejects("\"rewards\": [\"loot\"] }", "\"rewards\": [\"gold\"] }", "an unknown reward kind");
        Rejects("\"id\": \"rogue-a\"", "\"id\": \"shaman-a\"", "a duplicate boss id");
        Rejects("\"rounds\": [7], \"source\": \"fixture\"", "\"rounds\": [9], \"source\": \"fixture\"", "a boss round past the last round");
        Rejects("\"class\": \"WARRIOR\", \"title\": \"LOOTA_Warrior_07\"", "\"class\": \"PRIEST\", \"title\": \"LOOTA_Warrior_07\"", "a bucket of a class the run lacks");
        Rejects("\"CS2_182\", \"CS2_120\", \"CS2_171\"", "\"CS2_182\", \"CS2_182\", \"CS2_171\"", "a treasure listed twice");
        Rejects("[{ \"id\": \"CS2_171\", \"count\": 2 }, { \"id\": \"CS2_120\", \"count\": 2 }]",
            "[{ \"id\": \"CS2_171\", \"count\": 2 }, { \"id\": \"CS2_171\", \"count\": 2 }]", "a deck card listed twice");
        Rejects("\"hero\": \"HERO_05\"", "\"hero\": \"HERO 05\"", "an invalid card id");
        Rejects("\"id\": \"druid-a\"", "\"id\": \"Druid_A\"", "an invalid entry id");
        Rejects("\"family\": \"priest\", \"hero\": \"HERO_09\"", "\"family\": \"Priest Family\", \"hero\": \"HERO_09\"", "an invalid family");
        Rejects("{ \"class\": \"WARRIOR\", \"deck\"", "{ \"class\": \"WIZARD\", \"deck\"", "an unknown class");
    }
}
