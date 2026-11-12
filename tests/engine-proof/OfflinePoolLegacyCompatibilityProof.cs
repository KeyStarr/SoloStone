using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using Type = System.Type;

// The old random generators (Lackeys, Adapt, class and cost pools, Entourage, Discover and so on) read a frozen,
// ordered snapshot of cards (LegacyGenerationV1, 124 inputs), so registering or admitting new cards cannot change
// the random picks in the seeded golden games. legacy-generation-v1.sha256 pins the name, count and hash of every
// input; this proof fails if any input changes and injects fake new cards to show they never enter one. A failure
// after adding cards means a new card leaked into a legacy pool.
internal static class OfflinePoolLegacyCompatibilityProof
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("legacy generation: " + message);
    }

    private static object Field(Type type, string name) => type.GetField(name,
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);

    private static void Same(string name, IEnumerable<Card> actual) => Check(
        actual.Select(c => c.Id + ":" + c.AssetId).SequenceEqual(LegacyGenerationV1.IdentityText(name).Split('\n', StringSplitOptions.RemoveEmptyEntries)),
        "ordered consumer input changed: " + name);

    private static Game NewGame(FormatType format = FormatType.FT_WILD)
    {
        var game = new Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, Logging = false, History = true, FormatType = format });
        game.StartGame();
        return game;
    }

    internal static void Run()
    {
        ColdStart();
        var pins = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "legacy-generation-v1.sha256"))
            .Select(line => line.Split('\t')).ToDictionary(row => row[0]);
        Check(pins.Keys.ToHashSet().SetEquals(LegacyGenerationV1.Names), "input inventory changed");
        foreach (var pair in pins)
        {
            string text = LegacyGenerationV1.IdentityText(pair.Key);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            Check(hash == pair.Value[2], "fingerprint changed: " + pair.Key);
            var input = LegacyGenerationV1.Get(pair.Key);
            Check(input.Count == int.Parse(pair.Value[1]), "count changed: " + pair.Key);
            Same(pair.Key, input); // Also validates the resolved dbfId/cardId, not only XML bytes.
            Check(((IList<Card>)input).IsReadOnly, "mutable input: " + pair.Key);
        }

        // Inject definitions that match every raw bypass, with and without the legacy exclusion flag.
        var container = typeof(Cards).GetField("Data", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var definitions = (Dictionary<string, Card>)container.GetType().GetProperty("Cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(container);
        var added = new[] {
            Definition("LEGACY_FREEZE_UNGORO_TEST", CardType.MINION, CardSet.UNGORO, false),
            Definition("LEGACY_FREEZE_PAST_TEST", CardType.MINION, CardSet.NAXX, true),
            Definition("UNG_999t_FREEZE_TEST", CardType.SPELL, CardSet.UNGORO, false)
        };
        foreach (var card in added) definitions.Add(card.Id, card);
        try
        {
            Check(added.All(c => c.Collectible) && Cards.All.Count(c => added.Contains(c)) == 3, "probe definitions not installed");
            foreach (string name in LegacyGenerationV1.Names)
                Check(!LegacyGenerationV1.Get(name).Any(added.Contains), "new definition entered " + name);
            Same("Lackeys", (Card[])Field(typeof(AddLackeyTask), "Lackeys"));
            Same("Adapt", (Card[])Field(typeof(AdaptTask), "TotalAdaptCards"));
            Same("PastLegendary", (Card[])Field(typeof(SpecificTask.GetRandomPastLegendary), "PastLegendaryMinions"));
            SharedInputs();
            FixedInputs();
            EntourageConsumers();
            ReplaceAndPack();
            BeastPartitions();
        }
        finally { foreach (var card in added) definitions.Remove(card.Id); }
        Console.WriteLine($"PASS legacy generation: inputs={pins.Count}, ordered fingerprints; injected collectible definitions excluded; cold shared pools; shared/raw/fixed consumers, entourage identities/RNG, Replace RNG, Beast partitions");
    }

    private static void ColdStart()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath) {
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--legacy-pools-cold-child");
        using var child = Process.Start(start);
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(30000))
        {
            child.Kill(entireProcessTree: true);
            throw new Exception("legacy generation: cold-start proof timed out");
        }
        Check(child.ExitCode == 0, "cold-start proof: " + output.Result + error.Result);
        Console.Write(output.Result);
    }

    internal static void ColdStartChild()
    {
        // ClientOverlay has no Cards dependency: install definitions before Cards' static constructor runs.
        var added = new[] { CardSet.CORE, CardSet.UNGORO, CardSet.NAXX }.Select((set, i) =>
            new ClientOverlay.Definition {
                CardId = "LEGACY_COLD_" + set, DbfId = 900001 + i, Name = "Cold pool probe", Text = "",
                Tags = new Dictionary<string, int> {
                    ["202"] = (int)CardType.MINION, ["183"] = (int)set, ["321"] = 1,
                    ["199"] = (int)CardClass.HUNTER, ["200"] = (int)Race.BEAST,
                    ["203"] = (int)Rarity.LEGENDARY, ["48"] = 1, ["47"] = 1, ["45"] = 1
                }
            }).ToArray();
        ClientOverlay.Data.Cards = ClientOverlay.Data.Cards.Concat(added).ToArray();
        var installed = added.Select(d => Cards.FromId(d.CardId)).ToArray();
        Check(installed.All(c => c.Collectible && !c.ExcludeFromLegacyRandomPools &&
            c.Type == CardType.MINION && c.Class == CardClass.HUNTER && c.Cost == 1),
            "cold definitions must qualify for unfrozen Wild/class/cost pools");
        Check(Cards.StandardSets.Contains(installed[0].Set), "cold Standard candidate missing");
        SharedInputs();
        Console.WriteLine("PASS legacy generation cold start: added Standard/Wild definitions excluded from ordered shared/class/cost pools");
    }

    private static Card Definition(string id, CardType type, CardSet set, bool excluded) => new Card(id, -100010 - (int)set - (int)type,
        new[] { new Tag(GameTag.CARDTYPE, (int)type), new Tag(GameTag.CARD_SET, (int)set),
            new Tag(GameTag.CLASS, (int)CardClass.HUNTER), new Tag(GameTag.CARDRACE, (int)Race.BEAST),
            new Tag(GameTag.COLLECTIBLE, 1), new Tag(GameTag.RARITY, (int)Rarity.LEGENDARY),
            new Tag(GameTag.MARK_OF_EVIL, 1), new Tag(GameTag.COST, 1), new Tag(GameTag.ATK, 1), new Tag(GameTag.HEALTH, 1) },
        new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>()) { ExcludeFromLegacyRandomPools = excluded };

    private static void SharedInputs()
    {
        Same("AllStandard", Cards.AllStandard);
        Same("AllWild", Cards.AllWild);
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            string prefix = LegacyGenerationV1.Format(format);
            var classes = Cards.FormatTypeClassCards(format);
            var costs = Cards.CostMinionCards(format);
            var frozenClasses = LegacyGenerationV1.Names.Where(n => n.StartsWith(prefix + "/Class/"))
                .Select(n => (CardClass)Enum.Parse(typeof(CardClass), n.Substring((prefix + "/Class/").Length))).ToArray();
            // Modern live pools are proved separately; the historical Wild DK resource stays fingerprint-pinned above.
            Check(!frozenClasses.Contains(CardClass.DEMONHUNTER), "DH unexpectedly entered frozen inventory");
            Check(classes.Keys.ToHashSet().SetEquals(frozenClasses.Append(CardClass.DEMONHUNTER).Append(CardClass.DEATHKNIGHT)), "class keys changed");
            Check(costs.Count == LegacyGenerationV1.Names.Count(n => n.StartsWith(prefix + "/Cost/")), "cost keys changed");
            foreach (var cls in frozenClasses.Where(cls => cls != CardClass.DEATHKNIGHT))
                Same(prefix + "/Class/" + cls, classes[cls]);
            foreach (var pair in costs) Same(prefix + "/Cost/" + pair.Key, pair.Value);
        }
    }

    private static void FixedInputs()
    {
        Same("BasicTotems", Cards.BasicTotems);
        var potions = (Card[][])Field(typeof(KazakusPower), "PotionCards");
        for (int i = 0; i < potions.Length; i++) Same("Potion/" + i, potions[i]);
        Same("JadeGolems", ((IReadOnlyList<string>)Field(typeof(ComplexTask), "JadeGolemStr")).Select(Cards.FromId));
        foreach (string name in LegacyGenerationV1.Names.Where(n => n.StartsWith("Entourage/")))
        {
            var source = Cards.FromId(name.Substring("Entourage/".Length));
            string[] original = source.Entourage;
            try
            {
                source.Entourage = original.Append("LEGACY_FREEZE_UNGORO_TEST").ToArray();
                Same(name, LegacyGenerationV1.Entourage(source).Select(Cards.FromId));
            }
            finally { source.Entourage = original; }
        }
    }

    private static void WithChangedEntourage(string sourceId, Action<Card, string[]> run)
    {
        var source = Cards.FromId(sourceId);
        string[] original = source.Entourage;
        string[] pinned = LegacyGenerationV1.Ids("Entourage/" + sourceId);
        try
        {
            // Rotate every position while keeping valid, distinct identities and the original size.
            source.Entourage = pinned.Skip(1).Concat(pinned.Take(1)).ToArray();
            Check(!source.Entourage.SequenceEqual(pinned), "entourage fixture must differ: " + sourceId);
            run(source, pinned);
        }
        finally { source.Entourage = original; }
    }

    private static void EntourageConsumers()
    {
        WithChangedEntourage("EX1_572", (card, pinned) => {
            foreach (int count in new[] { 1, 3 })
            foreach (bool opponent in new[] { false, true })
            {
                var game = NewGame();
                var source = Entity.FromCard(game.Player1, card);
                var rng = new Util.DeepCloneableRandom(17);
                var expected = new List<string>();
                while (expected.Count < count)
                {
                    string id = pinned.Choose(rng);
                    if (!expected.Contains(id)) expected.Add(id);
                }
                game.SetRandomSeed(17);
                var stack = new TaskStack();
                Check(new RandomEntourageTask(count, opponent).Process(game, game.Player1, source, null, stack)
                    == TaskState.COMPLETE, "RandomEntourage status");
                Check(stack.Playables.Select(p => p.Card.Id).SequenceEqual(expected), "RandomEntourage identities");
                Check(stack.Playables.All(p => p.Controller == (opponent ? game.Player2 : game.Player1)), "RandomEntourage owner");
                Check(game.Random.CaptureState().Equals(rng.CaptureState()), "RandomEntourage RNG");
            }
        });
        foreach (string sourceId in new[] { "LOOT_278", "LOOT_285", "LOOT_286" })
            WithChangedEntourage(sourceId, (card, pinned) => {
                var game = NewGame();
                var rng = new Util.DeepCloneableRandom(17);
                string expected = pinned.Choose(rng);
                game.SetRandomSeed(17);
                // Entering hand executes the card's ChangeUnidentifiedTask trigger.
                Entity.FromCard(game.Player1, card, zone: game.Player1.HandZone);
                Check(game.Player1.HandZone.Last().Card.Id == expected, "ChangeUnidentified identity: " + sourceId);
                Check(game.Random.CaptureState().Equals(rng.CaptureState()), "ChangeUnidentified RNG: " + sourceId);
            });
        WithChangedEntourage("CS2_049", (card, pinned) => {
            foreach (bool occupied in new[] { false, true })
            {
                var game = NewGame();
                var source = Entity.FromCard(game.Player1, card);
                if (occupied) Entity.FromCard(game.Player1, Cards.FromId(pinned[0]), zone: game.Player1.BoardZone);
                var rng = new Util.DeepCloneableRandom(17);
                string expected = (occupied ? pinned.Skip(1).ToArray() : pinned).Choose(rng);
                game.SetRandomSeed(17);
                SpecificTask.TotemicCall.Process(game, game.Player1, source, null, new TaskStack());
                Check(game.Player1.BoardZone.Last().Card.Id == expected, "TotemicCall identity");
                Check(game.Random.CaptureState().Equals(rng.CaptureState()), "TotemicCall RNG");
            }
        });
        WithChangedEntourage("BOT_238p", (card, pinned) => {
            foreach (bool rotating in new[] { false, true })
            {
                var game = NewGame();
                var rng = new Util.DeepCloneableRandom(17);
                // Force a rejected first draw when rotating an existing Boom power.
                string first = pinned.Choose(new Util.DeepCloneableRandom(17));
                var source = rotating ? Entity.FromCard(game.Player1, Cards.FromId(first)) : (IPlayable)game.Player1.Hero;
                if (rotating) game.Player1.Hero.HeroPower = (HeroPower)source;
                string expected;
                do { expected = pinned.Choose(rng); } while (rotating && expected == first);
                game.SetRandomSeed(17);
                SpecificTask.GetRandomDrBoomHeroPower.Process(game, game.Player1, source, null, new TaskStack());
                Check(game.Player1.Hero.HeroPower.Card.Id == expected, "DrBoom identity");
                Check(game.Random.CaptureState().Equals(rng.CaptureState()), "DrBoom RNG");
            }
        });
    }

    private static void ReplaceAndPack()
    {
        var game = NewGame();
        var player = game.Player1;
        var input = LegacyGenerationV1.Get("Replace/LEGENDARY");
        Check(input.Any(c => c.ExcludeFromLegacyRandomPools), "Replace lost already-excluded batch members");
        // Select an already-excluded member deliberately, then compare the exact RNG cursor.
        long seed = 0;
        while (!input.Choose(new Util.DeepCloneableRandom(seed)).ExcludeFromLegacyRandomPools) seed++;
        var expectedRng = new Util.DeepCloneableRandom(seed);
        Card expected = input.Choose(expectedRng);
        game.SetRandomSeed(seed);
        var old = Entity.FromCard(player, Cards.FromId("CS2_231"), zone: player.HandZone);
        new ReplaceTask(EntityType.TARGET, Rarity.LEGENDARY).Process(game, player, player.Hero, old);
        Check(player.HandZone.Last().Card.Id == expected.Id, "Replace sample/order/exclusion changed");
        Check(game.Random.CaptureState().Equals(expectedRng.CaptureState()), "Replace RNG consumption changed");
        SpecificTask.UngoroPack.Process(game, player, player.Hero, null);
        var pack = (IReadOnlyDictionary<Rarity, Card[]>)Field(typeof(SpecificTask), "_ungoroPackMemory");
        foreach (var pair in pack) Same("Ungoro/" + pair.Key, pair.Value);
    }

    private static void BeastPartitions()
    {
        // Each game samples its own format's pools; PoolCacheIsolationProof pins the exact draws.
        foreach (bool legacy in new[] { true, false })
        foreach (var order in new[] { new[] { FormatType.FT_STANDARD, FormatType.FT_WILD }, new[] { FormatType.FT_WILD, FormatType.FT_STANDARD } })
        foreach (var format in order)
        {
            var game = NewGame(format);
            var source = Entity.FromCard(game.Player1, Cards.FromId("ICC_828p"));
            var task = legacy ? SpecificTask.BuildABeast : source.Card.Power.PowerTask;
            task.Process(game, game.Player1, source, null, new TaskStack());
            var choice = game.Player1.Choice;
            string prefix = LegacyGenerationV1.Format(format);
            Check(choice.Choices.All(id => LegacyGenerationV1.Get(prefix + "/Beasts/First").Contains(game.IdEntityDic[id].Card))
                && choice.NextChoice.Choices.All(id => LegacyGenerationV1.Get(prefix + "/Beasts/Second").Contains(game.IdEntityDic[id].Card)),
                $"Beast partitions follow the game's format: {format} after {order[0]}, legacy={legacy}");
        }
    }
}
