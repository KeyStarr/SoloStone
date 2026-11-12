using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PegasusGame;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineGame = SabberStoneCore.Model.Game;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // The prefab of the client's "spawn to deck" effect; a generated-shuffle scope must carry it, with no targets.
    private const string GeneratedShufflePrefab = "ReuseFX_Generic_SpawnToDeck_NoFX_CardFromScript_Super:cc0ba21e3e5306140870277e116b2c4b";
    private static int parentPlagueReplacementCalls;

    private static void AssertGeneratedShuffleAdmission(Capture capture)
        => Check(!NativeAdmission.ValidatePackets(capture.HistorySnapshots()
            .Select(p => (IReadOnlyList<PowerHistoryData>)p.List)).Any(f => f.Rule == "R1"),
            "generated shuffle respects effect-specific admission");

    // Cards generated and shuffled into a deck must reach the client in the shape its spawn-to-deck effect targets: an
    // anonymous FULL_ENTITY already in DECK, then SHOW/HIDE/TARGET, inside one spawn scope. A playtest (fixed
    // 2026-10-22) showed no shuffle animation for Cosmic Manifestations' Outcast shuffles because the older shape
    // (INVALID FULL + HIDE) is never picked up by that effect. Later cases cover batching several insertions into one
    // scope.
    private static void GeneratedShuffleHistory()
    {
        BatchedStoryShuffle();
        ShuffleBatchBarriers();
        PlagueAttackShuffle();
        FreshDeckShuffleHistory();
        ArrivalCastsWhenDrawn();
        GeneratedShufflePrivacy();
        KnownGeneratedShuffleReveals();
        DiscoveredShuffleCompletion();
        foreach (bool bot in new[] { false, true })
        foreach (var scenario in new[] { (Card: "JAIL_892", Middle: false, Count: 2),
            (Card: "JAIL_892", Middle: true, Count: 1), (Card: "LOE_002", Middle: false, Count: 1),
            (Card: "BRM_007", Middle: false, Count: 3), (Card: "GVG_056", Middle: false, Count: 1),
            (Card: "AT_035", Middle: false, Count: 3), (Card: "CAP_404", Middle: false, Count: 2) })
        {
            var (match, capture) = AnimationMatch(bot, "showcase-demon-hunter-outcast");
            var owner = match.Game.CurrentPlayer;
            bool opponentDeck = scenario.Card is "GVG_056" or "AT_035" or "CAP_404";
            var destination = opponentDeck ? owner.Opponent : owner;
            bool privateDeck = destination.PlayerId == 2;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            if (scenario.Middle) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(scenario.Card), zone: owner.HandZone);
            if (scenario.Middle) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var victim = EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var before = destination.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && (opponentDeck ? t.Target == null : t.Target?.Id == victim.Id)));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var generated = destination.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            string label = scenario.Card + ":middle=" + scenario.Middle + ":bot=" + bot;
            Check(generated.Length == scenario.Count, label + " shuffle outcome");
            var scopes = new Stack<PowerHistorySubSpellStart>();
            var blocks = new Stack<PowerHistoryStart>();
            foreach (var entry in entries)
            {
                if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
                if (entry.HasSubSpellStart)
                {
                    Check(blocks.Any(b => b.Source == source.Id && (int)b.Type == (int)BlockType.POWER), label + " shuffle inside source POWER");
                    scopes.Push(entry.SubSpellStart);
                }
                if (entry.HasFullEntity && generated.Any(c => c.Id == entry.FullEntity.Entity))
                {
                    Check(TagValue(entry.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK
                        && entry.FullEntity.Name == "", label + " anonymous FULL_ENTITY starts in DECK for SpawnToDeckSpell targeting");
                    Check(scopes.Count == 1 && scopes.Peek().SpellPrefabGuid == GeneratedShufflePrefab
                        && scopes.Peek().TargetEntityIds.Count == 0 && scopes.Peek().SourceEntityId == destination.Hero.Id,
                        label + " creation enclosed by zero-target native spawn effect");
                }
                if (entry.HasSubSpellEnd) { Check(scopes.Count > 0, label + " scope end"); scopes.Pop(); }
                if (entry.HasPowerEnd) { Check(scopes.Count == 0, label + " scope cannot cross block end"); blocks.Pop(); }
            }
            Check(scopes.Count == 0 && blocks.Count == 0, label + " balanced history");
            // Cosmic interleaves a damage hit before each card, so each insertion keeps its own scope.
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab)
                == (scenario.Card == "JAIL_892" ? scenario.Count : 1), label + " consecutive insertions share one scope on both viewers");
            foreach (var card in generated)
            {
                int full = entries.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == card.Id);
                int show = entries.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id);
                int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
                Check(full >= 0 && show > full && hide > show, label + " FULL/SHOW/HIDE order");
                var snapshot = entries[show].ShowEntity;
                Check(TagValue(snapshot.Tags, GameTag.ZONE) == (int)EngineZone.DECK, label + " reveal destination DECK");
                if (!privateDeck)
                {
                    Check(snapshot.Name == card.Card.Id && TagValue(snapshot.Tags, GameTag.CARDTYPE) == (int)card.Card.Type
                        && TagValue(snapshot.Tags, GameTag.DISPLAYED_CREATOR) == source.Id,
                        label + " friendly reveal has generated definition/type/creator");
                    Check(entries[hide + 1].HasMetaData && (int)entries[hide + 1].MetaData.Type == (int)MetaDataType.TARGET
                        && entries[hide + 1].MetaData.Info.SequenceEqual(new[] { card.Id })
                        && entries.Skip(hide + 2).Any(e => e.HasSubSpellEnd), label + " retail post-hide target inside open scope");
                }
                else Check(snapshot.Name == "" && HasOnlyHiddenTags(snapshot.Tags)
                    && entries.Where(e => SnapshotEntity(e) == card.Id).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                    label + " opponent identity and definition tags stay private");
                Check(entries[hide].HideEntity.Zone == (int)EngineZone.DECK && card[GameTag.REVEALED] == 0,
                    label + " hidden final deck; no gameplay reveal mutation");
                var finalTags = ReplayNativeEntityTags(entries, card.Id);
                Check(finalTags[(int)GameTag.ZONE] == (int)EngineZone.DECK, label + " client final deck residence");
            }
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        GeneratedShuffleBoundaries();
        Console.WriteLine("PASS generated shuffle: Cosmic Outcast/middle, Forgotten Torch, Gang Up copies, Iron Juggernaut/Beneath the Grounds opponent deck/preallocated batches; both views, boundaries, native spawn contract/privacy");
    }

    private static void BatchedStoryShuffle()
    {
        foreach (bool bot in new[] { false, true })
        foreach (int slots in new[] { 0, 1, 2, 10 })
        foreach (bool trigger in new[] { false, true })
        {
            if (trigger && slots != 10) continue;
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            int capacity = SabberStoneCore.Model.Zones.DeckZone.DeckMaximumCapcity;
            while (owner.DeckZone.Count < capacity - slots) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId("TLC_826"), zone: owner.HandZone);
            source.Cost = 0;
            if (trigger)
            {
                EngineEntity.FromCard(owner, Cards.FromId("DAL_719"), zone: owner.BoardZone);
                Check(match.Game.TriggerManager.HasShuffleIntoDeckTrigger, "shuffle trigger is active");
            }
            var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var cards = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            Check(cards.Length == slots, "Story respects available deck slots: " + slots);
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) == (slots == 0 ? 0 : 1),
                "Story batches its insertions into one scope: " + slots);
            // A real listener's effect is queued, so its TRIGGER rows follow the flights in engine order.
            if (trigger) Check(entries.Count(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.TRIGGER) == slots
                && entries.FindIndex(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.TRIGGER) > entries.FindIndex(e => e.HasSubSpellEnd),
                "Story shuffle listener fires once per card after the batched flights");
            foreach (var card in cards)
            {
                int full = entries.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == card.Id);
                int show = entries.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id);
                int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
                Check(full >= 0 && show > full && hide > show && entries[hide + 1].HasMetaData
                    && (bot ? entries[hide + 1].MetaData.Info.Count > 0 && !entries[hide + 1].MetaData.Info.Contains(source.Id)
                        : entries[hide + 1].MetaData.Info.SequenceEqual(new[] { card.Id })), "Story preserves per-card snapshots and authored targets");
                Check(entries[show].ShowEntity.Name == (bot ? "" : card.Card.Id), "Story preserves deck privacy");
            }
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
    }

    // PowerHistory.GeneratedShuffle joins a creation to the previous unflushed spawn scope only across tag changes
    // on that scope's own deck cards, in the same block, into the same deck.
    private static void ShuffleBatchBarriers()
    {
        foreach (bool reveal in new[] { false, true })
        foreach (string barrier in new[] { "none", "creator", "tag", "zone", "block", "reset", "flush", "listener", "other-deck" })
        {
            var (match, _) = AnimationMatch();
            var game = match.Game; var owner = game.Player1;
            var source = EngineEntity.FromCard(owner, Cards.FromId("TLC_826"), zone: owner.HandZone);
            var history = game.PowerHistory;
            history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
            int blockIndex = history.Full.Count - 1;
            IPlayable Shuffle(SabberStoneCore.Model.Entities.Controller destination)
            {
                var card = reveal ? EngineEntity.FromCard(destination, Cards.FromId("UNG_920t2"))
                    : EngineEntity.FromCard(destination, Cards.FromId("UNG_920t2"), new Dictionary<GameTag, int> {
                        [GameTag.ZONE] = (int)EngineZone.DECK, [GameTag.DISPLAYED_CREATOR] = source.Id });
                if (reveal) card[GameTag.DISPLAYED_CREATOR] = source.Id;
                Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(destination, source, card, reveal), "barrier setup shuffle");
                return card;
            }
            var first = Shuffle(owner);
            SabberStoneCore.Kettle.IPowerHistoryEntry entry = barrier switch {
                "creator" => SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(first.Id, GameTag.DISPLAYED_CREATOR, source.Id),
                "tag" => SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.COST, 1),
                "zone" => SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(first.Id, GameTag.ZONE, (int)EngineZone.HAND),
                "block" => SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0),
                "reset" => SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(game),
                _ => null
            };
            if (entry != null) { history.Last.Add(entry); history.Full.Add(entry); }
            if (barrier == "flush") history.Last.Clear();
            if (barrier == "listener") game.TriggerManager.ShuffleIntoDeckTrigger += _ => { };
            var second = Shuffle(barrier == "other-deck" ? owner.Opponent : owner);
            var scopes = history.Full.Skip(blockIndex).OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>()
                .Where(e => e.SpellPrefabGuid == GeneratedShufflePrefab).ToArray();
            // A silent listener adds no rows, so it cannot separate flights; one that does is the tag/block barrier.
            bool joined = barrier is "none" or "creator" or "listener";
            Check(scopes.Length == (joined ? 1 : 2), "shuffle grouping respects " + barrier + " barrier; reveal=" + reveal);
            Check(history.Full.Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart)
                == history.Full.Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellEnd), barrier + " keeps scopes balanced; reveal=" + reveal);
            if (barrier != "flush")
            {
                int start = history.Last.IndexOf(scopes[0]);
                Check(start >= 0 && history.Full.Skip(history.Full.IndexOf(scopes[0])).Take(history.Last.Count - start)
                    .SequenceEqual(history.Last.Skip(start)), barrier + " keeps full and unflushed histories aligned; reveal=" + reveal);
            }
        }
    }

    private static void PlagueAttackShuffle()
    {
        PlagueShuffleListenerParity();
        foreach (bool bot in new[] { false, true })
        foreach (string variant in new[] { "normal", "one-slot", "full", "shielded", "silenced" })
        {
            var (match, capture) = AnimationMatch(bot);
            var attacker = match.Game.CurrentPlayer;
            var defender = attacker.Opponent;
            var plague = (Minion)EngineEntity.FromCard(attacker, Cards.FromId("JAIL_443"), zone: attacker.BoardZone);
            if (variant is "one-slot" or "full")
                while (defender.DeckZone.FreeSpace > (variant == "one-slot" ? 1 : 0))
                    EngineEntity.FromCard(defender, Cards.FromId("CS2_182"), zone: defender.DeckZone);
            if (variant == "shielded") defender.Hero.HasDivineShield = true;
            if (variant == "silenced") plague.Silence();
            plague.IsExhausted = false;
            int before = defender.DeckZone.Count;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var attack = attacker.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                && t.Source?.Id == plague.Id && t.Target?.Id == defender.Hero.Id);
            Check(attack != null, "legal Plague hero attack: " + variant + "/bot=" + bot);
            InvokeProcess(match, attack);
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var blights = defender.DeckZone.Where(c => c.Card.Id == "JAIL_443t").ToArray();
            int expected = variant == "normal" ? 8 : variant == "one-slot" ? 1 : 0;
            Check(blights.Length == expected && defender.DeckZone.Count == before + expected
                && defender.Hero.Damage == (variant == "silenced" ? 8 : 0),
                "Plague attack outcome for " + variant + " on either seat");
            Check(entries.Count(e => e.HasShuffleDeck && e.ShuffleDeck.PlayerId == defender.PlayerId) == (expected > 0 ? 1 : 0)
                && (expected == 0 || entries.FindIndex(e => e.HasShuffleDeck) > entries.FindLastIndex(e => e.HasHideEntity
                    && blights.Any(c => c.Id == e.HideEntity.Entity))),
                "Plague attack sends one deck-pile cue only after successful hidden insertions");
            Check(entries.Where(e => blights.Any(c => c.Id == SnapshotEntity(e)))
                .All(e => string.IsNullOrEmpty(SnapshotName(e))), "Blight identity remains hidden on wire");
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab)
                == (expected > 0 ? 1 : 0), "Plague bulk card-back flight on either viewer: " + variant + "/bot=" + bot);
            var blocks = new Stack<PowerHistoryStart>();
            int flights = 0;
            foreach (var entry in entries)
            {
                if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
                if (entry.HasFullEntity && blights.Any(c => c.Id == entry.FullEntity.Entity))
                {
                    Check(blocks.Any(b => b.Source == plague.Id && (int)b.Type == (int)BlockType.TRIGGER)
                        && TagValue(entry.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK
                        && HasOnlyHiddenTags(entry.FullEntity.Tags), "Plague fresh DECK FULL in causal TRIGGER, private definition");
                    flights++;
                }
                if (entry.HasPowerEnd) blocks.Pop();
            }
            Check(flights == expected && blocks.Count == 0, "every successful Blight gets a flight target");
            foreach (var card in blights)
            {
                int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
                Check(hide >= 0 && entries[hide + 1].HasMetaData
                    && (int)entries[hide + 1].MetaData.Type == (int)MetaDataType.TARGET
                    && entries[hide + 1].MetaData.Data == 0 && entries[hide + 1].MetaData.Info.SequenceEqual(new[] { card.Id }),
                    "Plague anonymous card has exact post-HIDE flight target on both viewers");
            }
            if (expected > 0)
            {
                var spawn = entries.Single(e => e.HasSubSpellStart).SubSpellStart;
                Check(spawn.SourceEntityId == defender.Hero.Id && spawn.TargetEntityIds.Count == 0,
                    "Plague native spawn discovers DECK FULLs from public destination hero");
            }
            if (!bot && variant == "normal")
            {
                var snapshot = match.Game.SaveSnapshot();
                var restored = EngineGame.LoadSnapshot(snapshot);
                Check(snapshot.SequenceEqual(restored.SaveSnapshot())
                    && restored.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryShuffleDeck>()
                        .Any(e => e.PlayerId == defender.PlayerId),
                    "Plague shuffle history survives snapshot roundtrip");
            }
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        foreach (int seat in new[] { 1, 2 })
        foreach (bool ownHero in new[] { false, true })
        {
            var game = new EngineGame(new GameConfig { RandomSeed = 443, StartPlayer = seat, SkipMulligan = true,
                History = false, Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 10).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 10).ToList() });
            game.StartGame();
            var owner = game.CurrentPlayer;
            var plague = (Minion)EngineEntity.FromCard(owner, Cards.FromId("JAIL_443"), zone: owner.BoardZone);
            var destination = ownHero ? owner : owner.Opponent;
            int before = destination.DeckZone.Count;
            destination.Hero.TakeDamage(plague, 8);
            Check(destination.DeckZone.Count == before + 8 && destination.Hero.Damage == 0,
                "Plague inserts Blights in historyless simulation for both seats and hero owners");
        }
        Console.WriteLine("PASS Plague attack shuffle: both seats, eight/one/zero inserts, Shield, Silence and private wire identities");
    }

    private static void PlagueShuffleListenerParity()
    {
        var replace = typeof(EngineGame).Assembly.GetType("SabberStoneCore.CardSets.Client.Sets.TheLivingPlague")
            .GetMethod("Replace", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var parent = new Harmony("offline.tests.plague-parent");
        foreach (bool bot in new[] { false, true })
        foreach (string variant in new[] { "listener", "killed", "silenced", "killed-and-silenced", "already-dead" })
        {
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game;
            var owner = game.CurrentPlayer;
            var plague = (Minion)EngineEntity.FromCard(owner, Cards.FromId("JAIL_443"), zone: owner.BoardZone);
            if (variant == "already-dead")
            {
                plague.Destroy();
                game.DeathProcessingAndAuraUpdate();
                Check(plague.IsDead && plague.Zone.Type == EngineZone.GRAVEYARD, "Plague fixture has already-dead source");
            }
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var baseline = game.CloneExact(history: true);
            var baselinePlague = (Minion)baseline.IdEntityDic[plague.Id];
            int before = owner.Opponent.DeckZone.Count;
            var order = new List<string>(); var baselineOrder = new List<string>();
            var trace = new List<string>(); var baselineTrace = new List<string>();
            void Listen(EngineGame target, Minion source, List<string> callbacks, List<string> states)
            {
                int inserts = 0;
                void Record(string listener, SabberStoneCore.Model.Entities.IEntity card)
                {
                    callbacks.Add(listener + ":" + card.Id);
                    states.Add(listener + ":" + card.Id + ":" + source.IsDead + ":" + source.IsSilenced + ":" + source.Zone.Type
                        + ":" + string.Join(",", source.Controller.Opponent.DeckZone.Select(c => c.Id))
                        + ":" + MatchRecorder.RandomState(target));
                }
                target.TriggerManager.ShuffleIntoDeckTrigger += card => {
                    Record("first", card);
                    if (++inserts != 1) return;
                    if (variant is "silenced" or "killed-and-silenced") source.Silence();
                    if (variant is "killed" or "killed-and-silenced")
                    {
                        source.Destroy();
                        target.DeathProcessingAndAuraUpdate();
                    }
                };
                target.TriggerManager.ShuffleIntoDeckTrigger += card => Record("second", card);
                Check(target.TriggerManager.HasShuffleIntoDeckTrigger, "Plague shuffle listener is active");
            }
            Listen(game, plague, order, trace);
            Listen(baseline, baselinePlague, baselineOrder, baselineTrace);
            string label = "Plague " + variant + "/bot=" + bot;
            Check(game.Hash() == baseline.Hash() && MatchRecorder.RandomState(game) == MatchRecorder.RandomState(baseline),
                label + " starts from identical engine state and RNG");
            Check(owner.Opponent.Hero.TakeDamage(plague, 8) == 0, label + " replaces hero damage");
            // Run the same damage path with the pre-fix replacement body, restoring the hook even on failure.
            parentPlagueReplacementCalls = 0;
            parent.Patch(replace, prefix: new HarmonyMethod(typeof(Program), nameof(ParentPlagueReplacement)));
            try { Check(baseline.CurrentPlayer.Opponent.Hero.TakeDamage(baselinePlague, 8) == 0, label + " parent replaces hero damage"); }
            finally { parent.Unpatch(replace, HarmonyPatchType.Prefix, parent.Id); }
            Check(parentPlagueReplacementCalls == 1, label + " executes frozen parent oracle");
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var blights = owner.Opponent.DeckZone.Where(c => c.Card.Id == "JAIL_443t").OrderBy(c => c.Id).ToArray();
            Check(blights.Length == 8 && owner.Opponent.DeckZone.Count == before + 8 && owner.Opponent.Hero.Damage == 0,
                label + " completes all eight insertions despite source mutation");
            Check(order.SequenceEqual(blights.SelectMany(c => new[] { "first:" + c.Id, "second:" + c.Id }))
                && order.SequenceEqual(baselineOrder) && trace.SequenceEqual(baselineTrace),
                label + " callbacks run in parent insertion/subscription order with identical intermediate deck and RNG");
            Check(owner.Opponent.DeckZone.Select(c => (c.Id, c.Card.Id))
                .SequenceEqual(baseline.CurrentPlayer.Opponent.DeckZone.Select(c => (c.Id, c.Card.Id)))
                && MatchRecorder.RandomState(game) == MatchRecorder.RandomState(baseline)
                && game.Hash() == baseline.Hash(), label + " exact deck order, RNG and engine state equal parent");
            Check(plague.IsSilenced == (variant is "silenced" or "killed-and-silenced")
                && (plague.Zone.Type == EngineZone.GRAVEYARD) == (variant is "killed" or "killed-and-silenced" or "already-dead"),
                label + " source mutation actually occurred");
            AssertFreshHiddenFlights(entries, blights, variant is "listener" or "already-dead" ? 1 : 2, label);
            // A silent listener cannot split the flights; the rows of a first-insert death or silence do, once.
            int scopes = 0, total = 0, fulls = 0;
            foreach (var entry in entries)
            {
                if (entry.HasSubSpellStart && entry.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab)
                {
                    Check(scopes == 0, label + " listener scopes do not nest");
                    scopes++; total++;
                }
                if (entry.HasFullEntity && blights.Any(c => c.Id == entry.FullEntity.Entity))
                {
                    Check(scopes == 1, label + " every insertion flies inside a spawn scope");
                    fulls++;
                }
                if (entry.HasSubSpellEnd) { Check(scopes == 1, label + " listener scope closes"); scopes--; }
            }
            Check(fulls == 8 && total == (variant is "listener" or "already-dead" ? 1 : 2), label + " scopes split only at rows the listener produced: " + total);
            Check(scopes == 0, label + " listener scopes balanced");
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Plague listener/source mutation: both seats, scopes split only at listener rows, callback/deck/RNG parity with parent, first-insert death/silence and already-dead source");
    }

    // Frozen gameplay oracle from e66fcff1^; only its presentation bookkeeping differed.
    private static bool ParentPlagueReplacement(IPlayable source, Hero hero, int damage, ref bool __result)
    {
        parentPlagueReplacementCalls++;
        __result = false;
        if (damage <= 0 || source.Card.Id != "JAIL_443" || source is not Minion minion || minion.IsSilenced) return false;
        int count = Math.Min(damage, hero.Controller.DeckZone.FreeSpace);
        var facts = typeof(EngineGame).GetProperty("Facts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(source.Game);
        using var shuffle = (IDisposable)facts.GetType().GetMethod("Shuffle",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(facts, new object[] { source.Controller.Id });
        int inserted = 0;
        for (int i = 0; i < count; i++)
            if (hero.Controller.DeckZone.AddGenerated(Cards.FromId("JAIL_443t"), source, shuffle: true) != null) inserted++;
        if (inserted > 0 && source.Game.History)
            source.Game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryShuffleDeck { PlayerId = hero.Controller.PlayerId });
        __result = true;
        return false;
    }

    private static void GeneratedShufflePrivacy()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string cardId in new[] { "BOT_087", "ICC_215" })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId(cardId), zone: owner.HandZone);
            int expected = cardId == "BOT_087" ? 10 : owner.Opponent.DeckZone.Count;
            var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && t.Target == null));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var generated = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            string label = cardId + ":bot=" + bot;
            Check(generated.Length == expected, label + " shuffles unknown cards");
            foreach (var card in generated)
            {
                var snapshots = entries.Where(e => SnapshotEntity(e) == card.Id).ToArray();
                Check(snapshots.Length > 0 && snapshots.All(e => string.IsNullOrEmpty(SnapshotName(e))),
                    label + " shuffled identity stays hidden from owner and opponent");
                Check(snapshots.All(e => HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags)),
                    label + " definition tags stay hidden");
                Check(entries.Any(e => e.HasFullEntity && e.FullEntity.Entity == card.Id
                    && TagValue(e.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK)
                    && entries.Any(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.TARGET
                        && (bot ? e.MetaData.Info.Count > 0 && !e.MetaData.Info.Contains(source.Id) : e.MetaData.Info.SequenceEqual(new[] { card.Id }))),
                    label + " hidden shuffle retains DECK FULL and authored spawn target");
            }
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab)
                == 1, label + " both viewers retain one batched spawn scope for hidden cards");
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
    }

    private static void FreshDeckShuffleHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            foreach (var recipe in new[] { (Source: "JAIL_442", Card: "JAIL_443t", Count: 4),
                (Source: "EDR_260", Card: "EDR_260t", Count: 2) })
            {
                var (match, capture) = AnimationMatch(bot);
                var owner = match.Game.CurrentPlayer;
                var body = (Minion)EngineEntity.FromCard(owner, Cards.FromId(recipe.Source), zone: owner.BoardZone);
                body.Damage = body.BaseHealth - 1;
                var fireball = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone);
                var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                InvokeProcess(match, PlayCardTask.SpellTarget(owner, fireball, body));
                FlushFieldHistory(match);
                var generated = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
                var entries = History(capture, cursor);
                Check(generated.Length == recipe.Count && generated.All(c => c.Card.Id == recipe.Card), recipe.Source + " real deathrattle shuffles");
                AssertFreshHiddenFlights(entries, generated, 1, recipe.Source + "/bot=" + bot);
                AssertGeneratedShuffleAdmission(capture); capture.SerializeAll();
            }
            foreach (var recipe in new[] { (Source: "TLC_602t", Count: 7), (Source: "UNG_920t1", Count: 20) })
            {
                var (match, capture) = AnimationMatch(bot);
                var owner = match.Game.CurrentPlayer;
                var source = EngineEntity.FromCard(owner, Cards.FromId(recipe.Source), zone: owner.HandZone);
                var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
                FlushFieldHistory(match);
                var generated = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
                Check(generated.Length == recipe.Count, recipe.Source + " real battlecry shuffles");
                AssertFreshHiddenFlights(History(capture, cursor), generated, 1, recipe.Source + "/bot=" + bot);
                AssertGeneratedShuffleAdmission(capture); capture.SerializeAll();
            }
            var (sog, sogCapture) = SogStart(bot ? Array.Empty<string>() : new[] { "JAIL_384", "EX1_012" },
                bot ? new[] { "JAIL_384", "EX1_012" } : Array.Empty<string>());
            var player = bot ? sog.Game.Player2 : sog.Game.Player1;
            var oldIds = sog.Game.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            SogComplete(sog, sogCapture);
            var copies = player.DeckZone.Where(c => !oldIds.Contains(c.Id) && c.Card.Id == "EX1_012").ToArray();
            Check(copies.Length == 1, "Hogger copies eligible legendary");
            AssertFreshHiddenFlights(SogHistory(sogCapture), copies, 1, "Hogger/bot=" + bot);
            sogCapture.SerializeAll();
            var (soul, soulCapture) = SogStart(bot ? Array.Empty<string>() : new[] { "CATA_EVENT_110" },
                bot ? new[] { "CATA_EVENT_110" } : Array.Empty<string>());
            var prior = soul.Game.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            SogComplete(soul, soulCapture);
            var essences = soul.Game.IdEntityDic.Values.Where(c => !prior.Contains(c.Id)
                && c.Card.Id.StartsWith("CATA_EVENT_110t", StringComparison.Ordinal)).ToArray();
            Check(essences.Select(c => c.Card.Id).OrderBy(id => id).SequenceEqual(
                Enumerable.Range(2, 6).Select(i => "CATA_EVENT_110t" + i)), "Dragon Soul creates all six distinct essences");
            var soulEntries = SogHistory(soulCapture);
            foreach (var essence in essences)
            {
                string label = "Dragon Soul " + essence.Card.Id + "/bot=" + bot;
                int full = soulEntries.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == essence.Id);
                int hide = soulEntries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == essence.Id);
                Check(full >= 0 && hide > full, label + " initial FULL precedes HIDE");
                // A later draw may legitimately SHOW the essence; only insertion snapshots must remain anonymous.
                var initial = soulEntries.Skip(full).Take(hide - full).Where(e => SnapshotEntity(e) == essence.Id).ToArray();
                Check(initial.All(e => string.IsNullOrEmpty(SnapshotName(e))
                    && TagValue(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK
                    && HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags)),
                    label + " initial FULL/SHOW anonymous DECK snapshots contain only hidden-tag allowlist");
                Check(soulEntries[hide].HideEntity.Zone == (int)EngineZone.DECK
                    && soulEntries[hide + 1].HasMetaData && (int)soulEntries[hide + 1].MetaData.Type == (int)MetaDataType.TARGET
                    && soulEntries[hide + 1].MetaData.Data == 0
                    && soulEntries[hide + 1].MetaData.Info.SequenceEqual(new[] { essence.Id })
                    && soulEntries.Skip(hide + 2).Any(e => e.HasSubSpellEnd), label + " HIDE followed by exact TARGET inside open scope");
            }
            Check(soulEntries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) == 1,
                "Dragon Soul fresh DECK creations fly as one six-card batch on either viewer");
            soulCapture.SerializeAll();
        }
        Console.WriteLine("PASS fresh DECK shuffles: Doctor, Greenwing, Hogger, Dragon Soul, Latorvius, Queen Carnassa; both viewers, hidden card flights");
    }

    private static void AssertFreshHiddenFlights(List<PowerHistoryData> entries, IPlayable[] cards, int scopes, string label)
    {
        Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) == scopes,
            label + " keeps native spawn scopes");
        foreach (var card in cards)
        {
            var snapshots = entries.Where(e => SnapshotEntity(e) == card.Id).ToArray();
            Check(snapshots.Any(e => e.HasFullEntity && TagValue(e.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK)
                && snapshots.All(e => string.IsNullOrEmpty(SnapshotName(e))
                    && HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags)), label + " anonymous DECK FULL, no identity or definition leak");
            int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
            Check(hide >= 0 && entries[hide + 1].HasMetaData && entries[hide + 1].MetaData.Info.SequenceEqual(new[] { card.Id }),
                label + " HIDE followed by exact flight target");
        }
    }

    private static void ArrivalCastsWhenDrawn()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var player = match.Game.CurrentPlayer;
            foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
            var gear = EngineEntity.FromCard(player, Cards.FromId("JAIL_386t"), zone: player.DeckZone);
            var spell = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.DeckZone);
            var minion = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.DeckZone);
            var arrival = EngineEntity.FromCard(player, Cards.FromId("BE_EVENT_101"), zone: player.HandZone);
            int hand = player.HandZone.Count, armor = player.Hero.Armor;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, PlayCardTask.Spell(player, arrival));
            FlushFieldHistory(match);
            Check(player.DeckZone.Count == 0 && player.HandZone.Contains(spell) && player.HandZone.Contains(minion)
                && !player.HandZone.Contains(gear) && player.Hero.Armor == armor + 2 && player.HandZone.Count == hand + 1,
                "Arrival: Gear consumes one spell slot, casts, draws ordinary minion replacement; other slot draws spell");
            var entries = History(capture, cursor);
            Check(entries.Any(e => e.HasPowerStart && e.PowerStart.Source == gear.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER)
                && entries.Any(e => e.HasShowEntity && e.ShowEntity.Entity == minion.Id), "Arrival replacement is logged on native wire");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Arrival + Found Gear: spell tutor slot consumed, +2 Armor, ordinary replacement and second spell, both seats");
    }

    private static void KnownGeneratedShuffleReveals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string cardId in new[] { "BE_153", "LOOT_542" })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId(cardId), zone: owner.HandZone);
            source.Cost = 0;
            var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && t.Target == null));
            if (cardId == "LOOT_542")
            {
                var replacement = EngineEntity.FromCard(owner, Cards.FromId("CS2_106"), zone: owner.HandZone);
                replacement.Cost = 0;
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                    && t.Source?.Id == replacement.Id));
            }
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var generated = owner.DeckZone.Where(c => !before.Contains(c.Id)).Single();
            string expected = cardId == "BE_153" ? "BE_153t" : "LOOT_542";
            string label = cardId + ":bot=" + bot;
            Check(generated.Card.Id == expected, label + " known card shuffled");
            int full = entries.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == generated.Id);
            int show = entries.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == generated.Id);
            int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == generated.Id);
            Check(full >= 0 && show > full && hide > show, label + " known shuffle retains FULL/SHOW/HIDE");
            Check(entries[show].ShowEntity.Name == (bot ? "" : expected), label + " known identity revealed only to owner");
            if (bot) Check(entries.Where(e => SnapshotEntity(e) == generated.Id)
                .All(e => string.IsNullOrEmpty(SnapshotName(e))), label + " no opponent snapshot leak");
            Check(generated[GameTag.REVEALED] == 0, label + " reveal does not alter gameplay visibility");
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        Console.WriteLine("PASS known generated shuffles: Sun's Reckoning and Kingsbane owner SHOW; opponent hidden");
    }

    private static void DiscoveredShuffleCompletion()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("DAL_736"), zone: owner.HandZone);
            source.Cost = 0;
            FlushFieldHistory(match);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && t.Target == null));
            var selected = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                int choice = owner.Choice.Choices.First();
                selected.Add(match.Game.IdEntityDic[choice].Card.Id);
                InvokeProcess(match, ChooseTask.Pick(owner, choice));
                FlushFieldHistory(match);
            }
            int finalChoice = owner.Choice.Choices.First();
            selected.Add(match.Game.IdEntityDic[finalChoice].Card.Id);
            var priorIds = match.Game.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            int cursor = capture.Packets.Count;
            InvokeProcess(match, ChooseTask.Pick(owner, finalChoice));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var generated = owner.DeckZone.ToArray();
            string label = "Elysiana:bot=" + bot;
            Check(owner.Choice == null && generated.Length == 10 && generated.All(c => !priorIds.Contains(c.Id)),
                label + " five choices complete with ten fresh copies");
            Check(generated.Select(c => c.Card.Id).OrderBy(id => id)
                .SequenceEqual(selected.SelectMany(id => new[] { id, id }).OrderBy(id => id)), label + " two copies per chosen card");
            var blocks = new Stack<PowerHistoryStart>();
            int scopes = 0;
            foreach (var entry in entries)
            {
                if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
                if (entry.HasSubSpellStart && entry.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) scopes++;
                if (entry.HasFullEntity && generated.Any(c => c.Id == entry.FullEntity.Entity))
                {
                    Check(TagValue(entry.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK,
                        label + " fresh copy FULL starts in DECK");
                    Check(blocks.Any(b => b.Source == source.Id && (int)b.Type == (int)BlockType.POWER),
                        label + " fresh copy inside causal Elysiana POWER");
                }
                if (entry.HasPowerEnd) { Check(blocks.Count > 0, label + " matched POWER end"); blocks.Pop(); }
            }
            Check(blocks.Count == 0 && scopes == 1, label + " completion closes causal block and batches all ten copies in one scope");
            foreach (var card in generated)
            {
                int show = entries.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id);
                Check(show >= 0 && entries[show].ShowEntity.Name == (bot ? "" : card.Card.Id),
                    label + " chosen identity revealed only to owner");
                int hide = entries.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
                Check(hide > show && entries[hide + 1].HasMetaData
                    && (int)entries[hide + 1].MetaData.Type == (int)MetaDataType.TARGET
                    && (bot ? entries[hide + 1].MetaData.Info.Count > 0 && !entries[hide + 1].MetaData.Info.Contains(source.Id)
                        : entries[hide + 1].MetaData.Info.SequenceEqual(new[] { card.Id })), label + " hide then authored or typed-candidate spawn target");
                if (bot) Check(entries.Where(e => SnapshotEntity(e) == card.Id)
                    .All(e => string.IsNullOrEmpty(SnapshotName(e))), label + " no opponent identity leak");
            }
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Elysiana completion: ten fresh DECK FULLs in one spawn scope in causal POWER; chosen owner identities only");
    }

    private static void GeneratedShuffleBoundaries()
    {
        int capacity = SabberStoneCore.Model.Zones.DeckZone.DeckMaximumCapcity;
        foreach (int size in new[] { capacity - 1, capacity })
        {
            var (match, capture) = AnimationMatch(deck: "showcase-demon-hunter-outcast");
            var owner = match.Game.Player1;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            while (owner.DeckZone.Count < size) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId("JAIL_892"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, PlayCardTask.SpellTarget(owner, source, owner.Opponent.Hero));
            FlushFieldHistory(match);
            Check(owner.DeckZone.Count == capacity && History(capture, cursor).Count(e => e.HasSubSpellStart
                && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) == capacity - size,
                "full deck animates only successful insertions: " + size);
            var returning = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            owner.DeckZone.Remove(owner.DeckZone.TopCard);
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            owner.HandZone.Remove(returning);
            Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(owner, source, returning), "return existing hand card");
            FlushFieldHistory(match);
            Check(!History(capture, cursor).Any(e => e.HasSubSpellStart || e.HasShowEntity && e.ShowEntity.Entity == returning.Id),
                "existing hand-to-deck card retains native transition without generated-card reveal");
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
    }
}
