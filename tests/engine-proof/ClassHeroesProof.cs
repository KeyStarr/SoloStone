using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class ClassHeroesProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static readonly CardClass[] Classes = {
        CardClass.WARRIOR, CardClass.SHAMAN, CardClass.ROGUE, CardClass.PALADIN, CardClass.HUNTER,
        CardClass.DRUID, CardClass.WARLOCK, CardClass.MAGE, CardClass.PRIEST, CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT
    };

    internal static void Run()
    {
        using var input = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var client = JsonDocument.Parse(gzip);
        var definitions = client.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(c => c.GetProperty("cardId").GetString());
        for (int i = 0; i < Classes.Length; i++)
        {
            string id = $"HERO_{i + 1:00}";
            var hero = Cards.HeroCard(Classes[i]);
            var power = Cards.FromAssetId(hero[GameTag.HERO_POWER]);
            var data = definitions[id + "bp"];
            Check(hero.Id == id && hero.Class == Classes[i] && hero.Health == 30, id + " canonical class/health");
            Check(hero.Implemented && power.Implemented, id + " hero and power implemented");
            Check(power.AssetId == data.GetProperty("dbfId").GetInt32()
                && power.Cost == data.GetProperty("cost").GetInt32()
                && power.Cost == (i == 9 ? 1 : 2), id + " pinned power identity/cost");
            if (i < 9) LegacyPower(Classes[i]);
        }
        Check(Enum.GetName(typeof(CardClass), 14) == "DEMONHUNTER", "client class value 14");
        Check(Cards.HeroClasses.SequenceEqual(new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.PALADIN,
            CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.MAGE, CardClass.PRIEST, CardClass.WARRIOR }),
            "legacy random class order unchanged");
        var illidan = Cards.FromId("HERO_10");
        Check(illidan.AssetId == 56550 && EffectModules.ByDbfId[56550] == illidan.Id
            && EffectModules.ByDbfId[60224] == "HERO_10bp" && EffectModules.ByDbfId[56723] == "HERO_10bpe",
            "canonical modern hero module registry");
        Check(!StandardSets.CollectibleIds.Contains(illidan.Id), "hero excluded from Standard corpus");
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
            Check(!Cards.IsFormatLegal(illidan, format) && !Cards.FormatTypeCards(format).Contains(illidan)
                && Cards.FormatTypeClassCards(format).Values.All(pool => !pool.Contains(illidan)), "hero excluded from deck/random pools");
        // Sir Finley's pool must retain its pre-DH contents and order.
        var discover = NewGame(CardClass.MAGE);
        var expectedPowers = Cards.HeroCards().Where(c => !c.ExcludeFromLegacyRandomPools && c != discover.Player1.Hero.Card)
            .Select(c => c[GameTag.HERO_POWER]).ToArray();
        new DiscoverTask(DiscoverType.BASIC_HEROPOWERS, 100).Process(discover, discover.Player1, discover.Player1.Hero, null);
        var offeredPowers = discover.Player1.Choice.Choices.Select(id => discover.IdEntityDic[id].Card.AssetId);
        Check(offeredPowers.SequenceEqual(expectedPowers.Distinct()), "legacy basic-power Discover pool unchanged");
        var seededDiscover = NewGame(CardClass.MAGE);
        var baseline = seededDiscover.CloneExact();
        var expectedChoices = DiscoverTask.GetChoices(new[] { expectedPowers.Distinct().Select(Cards.FromAssetId).ToArray() },
            3, baseline.Random).Select(c => c.AssetId).ToArray();
        new DiscoverTask(DiscoverType.BASIC_HEROPOWERS).Process(seededDiscover, seededDiscover.Player1,
            seededDiscover.Player1.Hero, null);
        Check(seededDiscover.Player1.Choice.Choices.Select(id => seededDiscover.IdEntityDic[id].Card.AssetId)
            .SequenceEqual(expectedChoices) && seededDiscover.Random.Next() == baseline.Random.Next(),
            "legacy basic-power Discover choices and RNG consumption unchanged");
        DemonHunterPools();
        DeathKnightPools();
        GhoulCharge();
        foreach (bool weapon in new[] { false, true }) DemonClaws(weapon);
        Console.WriteLine("PASS class heroes: 11 canonical heroes/basic powers; 9 legacy effects; DH pools/discover/random/transform/secrets/Glimmerroot; Demon Claws attack/weapon/expiry/reuse/clone/snapshot");
    }

    private static Game NewGame(CardClass heroClass, bool explicitHero = false, FormatType format = FormatType.FT_WILD, int seed = 123, string deckCard = "CS2_179")
    {
        var game = new Game(new GameConfig {
            RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Logging = false, History = true, FormatType = format,
            Player1HeroClass = heroClass, Player1HeroCard = explicitHero ? Cards.HeroCard(heroClass) : null,
            Player1Deck = Enumerable.Repeat(Cards.FromId(deckCard), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_179"), 30).ToList()
        });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }

    private static void DemonHunterPools()
    {
        var failures = new List<string>();
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            foreach (bool weapons in new[] { false, true })
            {
                try
                {
                    var game = NewGame(CardClass.DEMONHUNTER, format: format);
                    var p = game.Player1;
                    var task = weapons ? new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)
                        : new DiscoverTask();
                    var state = task.Process(game, p, p.Hero, null);
                    var offered = p.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
                    Check(state == TaskState.COMPLETE && offered.Length > 0,
                        "DH Discover has choices " + format);
                    Check(offered.All(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                        && (format != FormatType.FT_STANDARD || Cards.StandardSets.Contains(c.Set))
                        && (weapons ? c.Type == CardType.WEAPON && Cards.HeroClasses.Contains(c.Class)
                            : c.Class == CardClass.DEMONHUNTER || c.Class == CardClass.NEUTRAL)),
                        "DH Discover class/format/implementation " + format);
                }
                catch (Exception ex) { failures.Add(format + (weapons ? " weapons: " : " own class: ") + ex.Message); }
            }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
        var legacy = Cards.HeroClasses.SelectMany(cls => new[] { Cards.Standard[cls], Cards.Wild[cls] })
            .Select(pool => string.Join(",", pool.Select(c => c.Id))).ToArray();
        var legacyGame = NewGame(CardClass.ROGUE);
        var legacyChoices = Offer(legacyGame, legacyGame.Player1, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS));
        long legacyRng = legacyGame.Random.NextLong();
        var legacyMage = NewGame(CardClass.MAGE);
        var legacyMinions = Offer(legacyMage, legacyMage.Player1, new DiscoverTask(DiscoverType.MINION));
        long legacyMinionRng = legacyMage.Random.NextLong();
        Offer(legacyMage, legacyMage.Player1, new DiscoverTask(DiscoverType.OP_HERO));
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD, FormatType.FT_WILD })
        foreach (int seed in new[] { 7, 123 })
        {
            var expectedClass = Cards.All.Where(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                && c.Class == CardClass.DEMONHUNTER
                && (format != FormatType.FT_STANDARD || Cards.StandardSets.Contains(c.Set))).ToArray();
            string deckCard = expectedClass.FirstOrDefault()?.Id ?? "CS2_179";
            var game = NewGame(CardClass.DEMONHUNTER, format: format, seed: seed, deckCard: deckCard);
            var p = game.Player1;
            var pool = format == FormatType.FT_STANDARD ? p.Standard : p.Wild;
            Check(pool.All(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                && (c.Class == CardClass.DEMONHUNTER || c.Class == CardClass.NEUTRAL && c.MultiClassGroup == 0)
                && (format != FormatType.FT_STANDARD || Cards.StandardSets.Contains(c.Set))), "DH controller pool legality");
            // Independent integration contract: losing implementation status must not shrink both sides silently.
            var requiredClassIds = new[] { "CORE_BT_351", "CORE_WC_701", "CORE_BT_801", "CORE_TTN_843",
                "CORE_BT_416", "CORE_BT_510", "CORE_BT_493", "CORE_CATA_001" }
                .Concat(format == FormatType.FT_WILD ? ("BE_564 BE_873 BE_877 CATA_525 CATA_526 CATA_528 CATA_530 "
                    + "DINO_138 EDR_842 EDR_890 EDR_891 EDR_892 FIR_904 JAIL_890 TIME_449 TLC_630 TLC_631 TLC_833 TLC_840 TLC_901 TLC_902 TLC_903").Split(' ')
                    : Array.Empty<string>());
            Check(requiredClassIds.All(id => pool.Any(c => c.Id == id)), "DH pool retains independently required integration IDs");
            Check(pool.Where(c => c.Class == CardClass.DEMONHUNTER).SequenceEqual(expectedClass),
                "DH own pool includes implemented collectible, excludes hero/unimplemented");
            foreach (var type in new[] { CardType.SPELL, CardType.MINION, CardType.HERO })
                CheckDiscover(game, p, new DiscoverTask(type, CardClass.DEMONHUNTER),
                    expectedClass.Where(c => c.Type == type).ToArray(), "explicit typed DH Discover");
            CheckDiscover(game, p, new DiscoverTask(DiscoverType.MINION),
                pool.Where(c => c.Type == CardType.MINION && !c.IsQuest).ToArray(),
                "typed Discover ignores warmed legacy caches and uses DH/neutral pool");
            CheckDiscover(game, p.Opponent, new DiscoverTask(DiscoverType.OP_HERO),
                expectedClass.Where(c => !c.IsQuest).ToArray(), "opponent DH Discover");
            var opponent = Offer(game, p.Opponent, new DiscoverTask(cardClass: CardClass.OP_CLASS));
            Check(opponent.Length == 3 && opponent.All(c => c.Class == CardClass.DEMONHUNTER || c.Class == CardClass.NEUTRAL),
                "opponent class criterion resolves DH");
            var another = Offer(game, p, new DiscoverTask(CardType.SPELL, CardClass.ANOTHER_CLASS));
            Check(another.Length == 3 && another.All(c => Cards.HeroClasses.Contains(c.Class) && c.Implemented),
                "DH another class uses legacy nine");
            var stack = new TaskStack();
            var expectedRandom = expectedClass.Where(c => !c.IsQuest).ToArray();
            foreach (var owner in new[] { p, p.Opponent })
            {
                var type = owner == p ? EntityType.HERO : EntityType.OP_HERO;
                Check(new RandomCardTask(type).Process(game, owner, owner.Hero, null, stack)
                    == (expectedRandom.Length == 0 ? TaskState.STOP : TaskState.COMPLETE)
                    && stack.Playables.Count == (expectedRandom.Length == 0 ? 0 : 1)
                    && stack.Playables.All(c => expectedRandom.Contains(c.Card)), "random own/opponent DH card");
            }
            foreach (var type in new[] { CardType.MINION, CardType.SPELL, CardType.HERO })
            {
                var expectedTyped = expectedRandom.Where(c => c.Type == type).ToArray();
                var expectedState = expectedTyped.Length == 0 ? TaskState.STOP : TaskState.COMPLETE;
                stack.Playables = new[] { Entity.FromCard(p, Cards.FromId("CS2_179")) };
                Check(new RandomCardTask(type, CardClass.DEMONHUNTER).Process(game, p, p.Hero, null, stack) == expectedState
                    && stack.Playables.Count == (expectedTyped.Length == 0 ? 0 : 1)
                    && stack.Playables.All(c => expectedTyped.Contains(c.Card)), "typed DH random pool/empty stack");
                var handBefore = p.Opponent.HandZone.Select(c => c.Card.Id).ToArray();
                Check(new ChangeEntityTask(EntityType.HAND, type, opClass: true)
                    .Process(game, p.Opponent, p.Opponent.Hero, null) == expectedState
                    && p.Opponent.HandZone.Count == handBefore.Length
                    && (expectedTyped.Length == 0
                        ? p.Opponent.HandZone.Select(c => c.Card.Id).SequenceEqual(handBefore)
                        : p.Opponent.HandZone.All(c => expectedTyped.Contains(c.Card))), "opponent typed DH transform");
            }
            SpecificTask.CastRandomSecret(CardClass.DEMONHUNTER).Process(game, p, p.Hero, null);
            Check(p.SecretZone.Count == 0, "DH without secrets is no-op");
            var glimmerroot = Offer(game, p.Opponent, SpecificTask.CuriousGlimmerroot);
            Check(glimmerroot.Length == Math.Min(3, expectedClass.Length)
                && glimmerroot.Distinct().Count() == glimmerroot.Length
                && glimmerroot.All(expectedClass.Contains)
                && glimmerroot.Count(c => c.Id == deckCard) == (expectedClass.Length == 0 ? 0 : 1),
                "Glimmerroot DH deck offers actual card and distinct legal decoys");
            var noClassDeck = NewGame(CardClass.DEMONHUNTER, format: format, seed: seed);
            Check(Offer(noClassDeck, noClassDeck.Player2, SpecificTask.CuriousGlimmerroot).Length == 0,
                "Glimmerroot does not reuse another DH deck cache");
        }
        foreach (string id in new[] { "CORE_KAR_069", "CORE_CFM_781" })
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        foreach (int seed in Enumerable.Range(1, 16))
        {
            var game = NewGame(CardClass.DEMONHUNTER, format: format, seed: seed);
            var p = game.Player1;
            foreach (var opening in p.HandZone.ToArray()) { p.HandZone.Remove(opening); p.SetasideZone.Add(opening); }
            var source = (Minion)Core2026Proof.Play(game, p, id);
            if (id == "CORE_CFM_781") Core2026Proof.Attack(game, source, p.Opponent.Hero);
            Check(p.HandZone.Count == 1 && p.HandZone.All(c => c.Card.Implemented),
                "DH client another-class generation: " + id);
            // Batch 11 uses declared Standard pools, which include Death Knight cards.
            CoreLegacy.CorePoolProof.Generated(game, p, id, p.HandZone.Single());
        }
        var after = NewGame(CardClass.ROGUE);
        Check(Offer(after, after.Player1, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)).SequenceEqual(legacyChoices)
            && after.Random.NextLong() == legacyRng, "DH leaves legacy weapon choices/RNG unchanged");
        var afterMage = NewGame(CardClass.MAGE);
        Check(Offer(afterMage, afterMage.Player1, new DiscoverTask(DiscoverType.MINION)).SequenceEqual(legacyMinions)
            && afterMage.Random.NextLong() == legacyMinionRng, "DH leaves legacy typed Discover cache/RNG unchanged");
        Check(legacy.SequenceEqual(Cards.HeroClasses.SelectMany(cls => new[] { Cards.Standard[cls], Cards.Wild[cls] })
            .Select(pool => string.Join(",", pool.Select(c => c.Id)))), "DH leaves legacy class pools unchanged");
    }

    private static void DeathKnightPools()
    {
        DeathKnightRandomMinionCache();
        DeathKnightThreeRunePools();
        var failures = new List<string>();
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            foreach (bool weapons in new[] { false, true })
            {
                try
                {
                    var game = NewGame(CardClass.DEATHKNIGHT, format: format);
                    var p = game.Player1;
                    var task = weapons ? new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)
                        : new DiscoverTask();
                    var state = task.Process(game, p, p.Hero, null);
                    var offered = p.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
                    Check(state == TaskState.COMPLETE && offered.Length > 0,
                        "DK Discover has choices " + format);
                    Check(offered.All(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                        && (format != FormatType.FT_STANDARD || (weapons ? Cards.StandardSets.Contains(c.Set) : Cards.Standard[CardClass.DEATHKNIGHT].Contains(c)))
                        && (weapons ? c.Type == CardType.WEAPON && Cards.HeroClasses.Contains(c.Class)
                            : CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT))),
                        "DK Discover class/format/implementation " + format);
                }
                catch (Exception ex) { failures.Add(format + (weapons ? " weapons: " : " own class: ") + ex.Message); }
            }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
        var legacy = Cards.HeroClasses.SelectMany(cls => new[] { Cards.Standard[cls], Cards.Wild[cls] })
            .Select(pool => string.Join(",", pool.Select(c => c.Id))).ToArray();
        var legacyGame = NewGame(CardClass.ROGUE);
        var legacyChoices = Offer(legacyGame, legacyGame.Player1, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS));
        long legacyRng = legacyGame.Random.NextLong();
        var legacyMage = NewGame(CardClass.MAGE);
        var legacyMinions = Offer(legacyMage, legacyMage.Player1, new DiscoverTask(DiscoverType.MINION));
        long legacyMinionRng = legacyMage.Random.NextLong();
        Offer(legacyMage, legacyMage.Player1, new DiscoverTask(DiscoverType.OP_HERO));
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD, FormatType.FT_WILD })
        foreach (int seed in new[] { 7, 123 })
        {
            var expectedClass = Cards.All.Where(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                && (c.Class == CardClass.DEATHKNIGHT || c[GameTag.MULTIPLE_CLASSES] != 0 && CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT))
                && Runes(c) < 3 && (format != FormatType.FT_STANDARD || StandardSets.CollectibleIds.Contains(c.Id))).ToArray();
            string deckCard = expectedClass.FirstOrDefault()?.Id ?? "CS2_179";
            var game = NewGame(CardClass.DEATHKNIGHT, format: format, seed: seed, deckCard: deckCard);
            var p = game.Player1;
            var pool = format == FormatType.FT_STANDARD ? p.Standard : p.Wild;
            Check(pool.All(c => c.Collectible && c.Implemented && c.Type != CardType.HERO
                && CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT)
                && (format != FormatType.FT_STANDARD || (c.Class == CardClass.NEUTRAL && c[GameTag.MULTIPLE_CLASSES] == 0 ? Cards.StandardSets.Contains(c.Set) : StandardSets.CollectibleIds.Contains(c.Id)))), "DK controller pool legality");
            // Independent integration contract: losing implementation status must not shrink both sides silently.
            var requiredClassIds = ("BE_284 BE_325 CATA_467 CATA_469 CORE_EDR_002 CORE_RLK_062 CORE_RLK_121 "
                + "EDR_812 EDR_816 JAIL_440 JAIL_441 JAIL_445 JAIL_450 JAIL_454 RLK_708 RLK_958 TIME_611 TLC_401 TLC_439 TLC_443 END_002 END_004").Split(' ');
            Check(requiredClassIds.All(id => pool.Any(c => c.Id == id)), "DK pool retains independently required integration IDs");
            Check(pool.Where(c => c.Class == CardClass.DEATHKNIGHT || c[GameTag.MULTIPLE_CLASSES] != 0).SequenceEqual(expectedClass),
                "DK own pool includes implemented collectible, excludes hero/unimplemented");
            foreach (var type in new[] { CardType.SPELL, CardType.MINION, CardType.HERO })
                CheckDiscover(game, p, new DiscoverTask(type, CardClass.DEATHKNIGHT),
                    expectedClass.Where(c => c.Type == type).ToArray(), "explicit typed DK Discover");
            CheckDiscover(game, p, new DiscoverTask(DiscoverType.MINION),
                pool.Where(c => c.Type == CardType.MINION && !c.IsQuest).ToArray(),
                "typed Discover ignores warmed legacy caches and uses DK/neutral pool");
            CheckDiscover(game, p.Opponent, new DiscoverTask(DiscoverType.OP_HERO),
                expectedClass.Where(c => !c.IsQuest).ToArray(), "opponent DK Discover");
            var opponent = Offer(game, p.Opponent, new DiscoverTask(cardClass: CardClass.OP_CLASS));
            Check(opponent.Length == 3 && opponent.All(c => CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT)),
                "opponent class criterion resolves DK");
            var another = Offer(game, p, new DiscoverTask(CardType.SPELL, CardClass.ANOTHER_CLASS));
            Check(another.Length == 3 && another.All(c => Cards.HeroClasses.Contains(c.Class) && c.Implemented),
                "DK another class uses legacy nine");
            var stack = new TaskStack();
            var expectedRandom = expectedClass.Where(c => !c.IsQuest).ToArray();
            foreach (var owner in new[] { p, p.Opponent })
            {
                var type = owner == p ? EntityType.HERO : EntityType.OP_HERO;
                Check(new RandomCardTask(type).Process(game, owner, owner.Hero, null, stack)
                    == (expectedRandom.Length == 0 ? TaskState.STOP : TaskState.COMPLETE)
                    && stack.Playables.Count == (expectedRandom.Length == 0 ? 0 : 1)
                    && stack.Playables.All(c => expectedRandom.Contains(c.Card)), "random own/opponent DK card");
            }
            foreach (var type in new[] { CardType.MINION, CardType.SPELL, CardType.HERO })
            {
                var expectedTyped = expectedRandom.Where(c => c.Type == type).ToArray();
                var expectedState = expectedTyped.Length == 0 ? TaskState.STOP : TaskState.COMPLETE;
                stack.Playables = new[] { Entity.FromCard(p, Cards.FromId("CS2_179")) };
                Check(new RandomCardTask(type, CardClass.DEATHKNIGHT).Process(game, p, p.Hero, null, stack) == expectedState
                    && stack.Playables.Count == (expectedTyped.Length == 0 ? 0 : 1)
                    && stack.Playables.All(c => expectedTyped.Contains(c.Card)), "typed DK random pool/empty stack");
                var handBefore = p.Opponent.HandZone.Select(c => c.Card.Id).ToArray();
                Check(new ChangeEntityTask(EntityType.HAND, type, opClass: true)
                    .Process(game, p.Opponent, p.Opponent.Hero, null) == expectedState
                    && p.Opponent.HandZone.Count == handBefore.Length
                    && (expectedTyped.Length == 0
                        ? p.Opponent.HandZone.Select(c => c.Card.Id).SequenceEqual(handBefore)
                        : p.Opponent.HandZone.All(c => expectedTyped.Contains(c.Card))), "opponent typed DK transform");
            }
            SpecificTask.CastRandomSecret(CardClass.DEATHKNIGHT).Process(game, p, p.Hero, null);
            Check(p.SecretZone.Count == 0, "DK without secrets is no-op");
            var glimmerroot = Offer(game, p.Opponent, SpecificTask.CuriousGlimmerroot);
            Check(glimmerroot.Length == Math.Min(3, expectedClass.Length)
                && glimmerroot.Distinct().Count() == glimmerroot.Length
                && glimmerroot.All(expectedClass.Contains)
                && glimmerroot.Count(c => c.Id == deckCard) == (expectedClass.Length == 0 ? 0 : 1),
                "Glimmerroot DK deck offers actual card and distinct legal decoys");
            var noClassDeck = NewGame(CardClass.DEATHKNIGHT, format: format, seed: seed);
            Check(Offer(noClassDeck, noClassDeck.Player2, SpecificTask.CuriousGlimmerroot).Length == 0,
                "Glimmerroot does not reuse another DK deck cache");
        }
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            var fill = new Game(new GameConfig { RandomSeed = 41, StartPlayer = 1, SkipMulligan = true,
                Logging = false, Player1HeroClass = CardClass.DEATHKNIGHT, Player2HeroClass = CardClass.DEATHKNIGHT,
                FormatType = format, FillDecks = true });
            fill.StartGame();
            Check(fill.Player1.DeckCards.Count == 30 && fill.Player2.DeckCards.Count == 30
                && fill.Player1.DeckCards.Concat(fill.Player2.DeckCards).All(c => CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT)),
                "DK DeckZone.Fill consumes isolated class pool " + format);
        }
        var multiDeck = NewGame(CardClass.DEATHKNIGHT, deckCard: "END_002");
        Check(Offer(multiDeck, multiDeck.Player2, SpecificTask.CuriousGlimmerroot).Any(c => c.Id == "END_002"),
            "DK Glimmerroot recognizes modern multi-class actual deck card");
        foreach (string id in new[] { "CORE_KAR_069", "CORE_CFM_781" })
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        foreach (int seed in Enumerable.Range(1, 16))
        {
            var game = NewGame(CardClass.DEATHKNIGHT, format: format, seed: seed);
            var p = game.Player1;
            foreach (var opening in p.HandZone.ToArray()) { p.HandZone.Remove(opening); p.SetasideZone.Add(opening); }
            var source = (Minion)Core2026Proof.Play(game, p, id);
            if (id == "CORE_CFM_781") Core2026Proof.Attack(game, source, p.Opponent.Hero);
            Check(p.HandZone.Count == 1 && p.HandZone.All(c => c.Card.Implemented),
                "DK client another-class generation: " + id);
            // Batch 11 uses declared Standard pools, which include Demon Hunter cards.
            CoreLegacy.CorePoolProof.Generated(game, p, id, p.HandZone.Single());
        }
        var after = NewGame(CardClass.ROGUE);
        Check(Offer(after, after.Player1, new DiscoverTask(DiscoverType.WEAPON_ANOTHERCLASS)).SequenceEqual(legacyChoices)
            && after.Random.NextLong() == legacyRng, "DK leaves legacy weapon choices/RNG unchanged");
        var afterMage = NewGame(CardClass.MAGE);
        Check(Offer(afterMage, afterMage.Player1, new DiscoverTask(DiscoverType.MINION)).SequenceEqual(legacyMinions)
            && afterMage.Random.NextLong() == legacyMinionRng, "DK leaves legacy typed Discover cache/RNG unchanged");
        Check(legacy.SequenceEqual(Cards.HeroClasses.SelectMany(cls => new[] { Cards.Standard[cls], Cards.Wild[cls] })
            .Select(pool => string.Join(",", pool.Select(c => c.Id)))), "DK leaves legacy class pools unchanged");
    }

    private static int Runes(Card c) => c[GameTag.COST_BLOOD] + c[GameTag.COST_FROST] + c[GameTag.COST_UNHOLY];

    // Retail 26.0.4: three-rune cards are never generated, matching the offline pool admission gate.
    private static void DeathKnightThreeRunePools()
    {
        var threeRune = new[] { "CORE_RLK_063", "CORE_RLK_706", "RLK_707" }.Select(Cards.FromId).ToArray();
        Check(threeRune.All(c => c.Collectible && c.Implemented && Runes(c) == 3
            && CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT)), "three-rune DK cards are registered deck cards");
        Check(Cards.Standard[CardClass.DEATHKNIGHT].Concat(Cards.Wild[CardClass.DEATHKNIGHT]).All(c => Runes(c) < 3),
            "legacy DK pools hold no three-rune card");
        Check(Cards.Standard[CardClass.DEATHKNIGHT].Any(c => Runes(c) == 2), "legacy DK pool keeps two-rune cards");
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            var game = NewGame(CardClass.DEATHKNIGHT, format: format);
            var generated = RandomCardTask.GetCardList(game.Player1.Hero, cardClass: CardClass.DEATHKNIGHT);
            Check(generated.Count > 0 && generated.All(c => Runes(c) < 3) && !generated.Intersect(threeRune).Any(),
                "RandomCardTask DK pool excludes three-rune cards " + format);
            foreach (int seed in Enumerable.Range(1, 40))
            {
                var seeded = NewGame(CardClass.DEATHKNIGHT, format: format, seed: seed);
                Check(Offer(seeded, seeded.Player1, new DiscoverTask()).All(c => Runes(c) < 3),
                    $"DK class Discover offers no three-rune card {format} seed={seed}");
            }
        }
        Console.WriteLine("PASS DK legacy pools/Discover/RandomCard exclude three-rune cards (CORE_RLK_063, CORE_RLK_706, RLK_707)");
    }

    private static void DeathKnightRandomMinionCache()
    {
        // One process, both class orders: each game uses its own class pool (no shared source cache).
        {
            foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
            foreach (bool dkFirst in new[] { false, true })
            foreach (int seed in Enumerable.Range(1, 8))
            {
                foreach (var cls in dkFirst ? new[] { CardClass.DEATHKNIGHT, CardClass.MAGE } : new[] { CardClass.MAGE, CardClass.DEATHKNIGHT })
                {
                    var game = NewGame(cls, format: format, seed: seed); var p = game.Player1;
                    var expectedPool = (format == FormatType.FT_STANDARD ? p.Standard : p.Wild)
                        .Where(c => c.Type == CardType.MINION && c[GameTag.RARITY] == (int)Rarity.LEGENDARY).ToList();
                    var rng = game.Random.Clone(); var expected = new List<string>();
                    for (int i = 0; i < 5; i++) { var pick = expectedPool.Choose(rng); expected.Add(pick.Id); expectedPool.Remove(pick); }
                    var stack = new TaskStack(); var source = Entity.FromCard(p, Cards.FromId("KAR_096"));
                    Check(new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY, 5,
                        classAndMultiOnlyFlag: true, maxInDeckFlag: true).Process(game, p, source, null, stack) == TaskState.COMPLETE
                        && stack.Playables.Select(c => c.Card.Id).SequenceEqual(expected)
                        && game.Random.CaptureState().Equals(rng.CaptureState()),
                        $"Malchezaar exact pool/results/RNG {format} DK-first={dkFirst} {cls} seed={seed}");
                }
            }
        }
        Console.WriteLine("PASS Malchezaar Mage-first/DK-first: exact class pools, legacy results and RNG in both formats");
    }

    private static void CheckDiscover(Game game, Controller player, ISimpleTask task, Card[] expected, string message)
    {
        player.Choice = null;
        var state = task.Process(game, player, player.Hero, null);
        var offered = player.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
        Check(state == (expected.Length == 0 ? TaskState.STOP : TaskState.COMPLETE)
            && offered.Length == Math.Min(3, expected.Length)
            && offered.Distinct().Count() == offered.Length && offered.All(expected.Contains), message);
    }

    private static Card[] Offer(Game game, Controller player, ISimpleTask task)
    {
        player.Choice = null;
        task.Process(game, player, player.Hero, null);
        return player.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
    }

    private static void GhoulCharge()
    {
        var hero = Cards.HeroCard(CardClass.DEATHKNIGHT);
        Check(hero.Id == "HERO_11" && hero.AssetId == 78065 && hero.Health == 30
            && hero[GameTag.HERO_POWER] == 78162 && hero.ExcludeFromLegacyRandomPools, "DeathKnightIdentity");
        Check(EffectModules.ByDbfId.Count(pair => pair.Value == "HERO_11bpt") == 1
            && EffectModules.ByDbfId[78164] == "HERO_11bpt", "GhoulRegistrationUnique");
        Check(!StandardSets.CollectibleIds.Contains(hero.Id) && !StandardSets.CollectibleIds.Contains("HERO_11bp"), "DK hero ordinary registration");
        foreach (bool explicitHero in new[] { false, true })
        {
            var game = CorpseProof.NewGame(CardClass.DEATHKNIGHT); var p = game.Player1;
            if (explicitHero) { game = NewGame(CardClass.DEATHKNIGHT, explicitHero: true); p = game.Player1; }
            var location = LocationProofs.Location(p);
            Core2026Proof.Summon(p, "CS2_182");
            Check(p.Hero.HeroPower.Cost == 2 && game.Process(HeroPowerTask.Any(p))
                && p.UsedMana == 2 && p.Hero.HeroPower.IsExhausted, "GhoulChargeCostsTwo");
            var ghoul = p.BoardZone.Last();
            Check(ghoul.Card.Id == "HERO_11bpt" && ghoul.AttackDamage == 1 && ghoul.Health == 1
                && ghoul.HasCharge && ghoul.Card.IsRace(Race.UNDEAD)
                && p.BoardZone.GetSlot(p.BoardZone.SlotCount - 1) == ghoul && location.ZonePosition == 0,
                "GhoulChargeSummonsChargeUndead at mixed-board right end");
            Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source == ghoul), "Ghoul can attack immediately");
            Core2026Proof.End(game);
            Check(ghoul.Zone == p.GraveyardZone && CorpseRules.Available(p) == (explicitHero ? 0 : 1), "GhoulDiesAtTurnEndAndGrantsCorpse when enabled");
        }
        foreach (bool locationSlot in new[] { false, true })
        {
            var game = CorpseProof.NewGame(CardClass.DEATHKNIGHT); var p = game.Player1;
            if (locationSlot) LocationProofs.Location(p);
            while (!p.BoardZone.IsFull) Core2026Proof.Summon(p, "CS2_182");
            string state = Canonical.State(game); int history = game.PowerHistory.Full.Count;
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER)
                && !game.Process(HeroPowerTask.Any(p)) && p.UsedMana == 0 && !p.Hero.HeroPower.IsExhausted,
                "GhoulChargeFullBoardRejected incl location=" + locationSlot);
            Check(Canonical.State(game) == state && game.PowerHistory.Full.Count == history, "full power rejection atomic");
        }
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
            Check(!Cards.FormatTypeCards(format).Contains(hero)
                && Cards.FormatTypeClassCards(format).Values.All(pool => !pool.Contains(hero)), "DK hero excluded from all generation pools");
    }

    private static void LegacyPower(CardClass heroClass)
    {
        var game = NewGame(heroClass);
        var p = game.Player1;
        p.Hero.Damage = 5;
        int hand = p.HandZone.Count;
        var target = heroClass == CardClass.PRIEST ? p.Hero : heroClass == CardClass.MAGE ? p.Opponent.Hero : null;
        Check(game.Process(HeroPowerTask.Any(p, target)), heroClass + " power accepted");
        Check(p.UsedMana == 2 && p.Hero.HeroPower.IsExhausted, heroClass + " mana/exhaustion");
        bool effect = heroClass switch {
            CardClass.WARRIOR => p.Hero.Armor == 2,
            CardClass.SHAMAN => p.BoardZone.Count == 1 && Cards.BasicTotems.Contains(p.BoardZone[0].Card),
            CardClass.ROGUE => p.Hero.Weapon?.Card.Id == "CS2_082" && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 2,
            CardClass.PALADIN => p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "CS2_101t" && p.BoardZone[0].AttackDamage == 1 && p.BoardZone[0].Health == 1,
            CardClass.HUNTER => p.Opponent.Hero.Damage == 2,
            CardClass.DRUID => p.Hero.AttackDamage == 1 && p.Hero.Armor == 1,
            CardClass.WARLOCK => p.HandZone.Count == hand + 1 && p.Hero.Damage == 7,
            CardClass.MAGE => p.Opponent.Hero.Damage == 1,
            CardClass.PRIEST => p.Hero.Damage == 3,
            _ => false
        };
        Check(effect, heroClass + " power effect sanity");
    }

    private static void DemonClaws(bool weapon)
    {
        var game = NewGame(CardClass.DEMONHUNTER, explicitHero: true);
        var p = game.Player1;
        if (weapon) Core2026Proof.Play(game, p, "CS2_082");
        p.UsedMana = 0;
        int attack = p.Hero.AttackDamage;
        Check(game.Process(HeroPowerTask.Any(p)), "Demon Claws accepted without target");
        Check(p.UsedMana == 1 && p.Hero.AttackDamage == attack + 1 && p.Hero.Armor == 0
            && p.Hero.HeroPower.IsExhausted, "Demon Claws mana/attack/no armor/exhaustion");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER), "no second power this turn");
        var bytes = game.SaveSnapshot();
        var clone = game.CloneExact(history: true);
        var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "DH snapshot byte stable");
        var continuations = new List<string>();
        foreach (var copy in new[] { game, clone, loaded })
        {
            int historyStart = copy.PowerHistory.Full.Count;
            var owner = copy.Player1;
            var hit = owner.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == owner.Opponent.Hero);
            Check(copy.Process(hit) && owner.Opponent.Hero.Damage == attack + 1, "DH legal attack includes weapon");
            Check(!owner.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK), "normal once-per-turn attack rule");
            Core2026Proof.End(copy);
            Check(owner.Hero[GameTag.ATK] == 0, "temporary attack expires at end of friendly turn");
            Core2026Proof.End(copy);
            Check(owner.Hero.AttackDamage == attack && !owner.Hero.HeroPower.IsExhausted, "next turn attack baseline/power reset");
            int mana = owner.UsedMana;
            Check(copy.Process(HeroPowerTask.Any(owner)) && owner.UsedMana == mana + 1
                && owner.Hero.AttackDamage == attack + 1, "Demon Claws usable next turn");
            continuations.Add(string.Join("\n", copy.PowerHistory.Full.Skip(historyStart).Select(e => Canonical.Value(e))));
        }
        Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded),
            "DH clone/snapshot continuation state");
        Check(continuations.Distinct().Count() == 1, "DH clone/snapshot continuation history");
        // Also exercise class-default construction instead of only explicit hero cards.
        Check(NewGame(CardClass.DEMONHUNTER).Player1.Hero.Card.Id == "HERO_10", "DH class-default game");
    }
}
