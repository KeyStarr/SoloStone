using System.Reflection;
using HsSim;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Zone = SabberStoneCore.Enums.Zone;

internal static class LocationProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Location " + message);
    internal static void Run()
    {
        var nonLocations = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Where(id => Cards.FromId(id).Type != CardType.LOCATION).ToArray();
        var batchCards = Simulation.DeckIds("standard-batch-1").SelectMany(id => Simulation.Deck(id).Cards).ToArray();
        Check(batchCards.Take(nonLocations.Length).SequenceEqual(nonLocations), "location pilots must preserve existing stress deck partitions");
        foreach (int seed in new[] { 31, 73 })
        for (int gap = 0; gap <= 2; gap++)
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            session.Flush();
            void Observe()
            {
                var violations = Invariants.Compare(game, session.Client).ToArray();
                Check(violations.Length == 0, string.Join("; ", violations.Select(v => v.ToString())));
                foreach (var player in new[] { game.Player1, game.Player2 })
                {
                    var board = session.Client.Entities.Where(p => p.Value.Get(GameTag.ZONE) == (int)Zone.PLAY
                        && p.Value.Get(GameTag.CONTROLLER) == player.PlayerId
                        && p.Value.Get(GameTag.CARDTYPE) is 4 or 39)
                        .OrderBy(p => p.Value.Get(GameTag.ZONE_POSITION)).Select(p => p.Key);
                    Check(board.SequenceEqual(player.BoardZone.Slots.Select(c => c.Id)), "physical order/ghosts");
                }
            }
            void Step(PlayerTask task)
            {
                session.PublishDecision(task.Controller);
                session.Process(task);
                Observe();
            }
            foreach (var player in new[] { game.Player1, game.Player2 })
            {
                player.BaseMana = 10; player.UsedMana = 0;
                for (int n = 0; n < 2; n++)
                    Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_182")), -1, null);
                var depths = (BoardObject)Generic.DrawCard(player, Cards.FromId("CORE_REV_990"));
                var network = (BoardObject)Generic.DrawCard(player, Cards.FromId("JAIL_877"));
                game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
                Step(player.Options().OfType<PlayCardTask>().Single(t => t.Source == depths && t.ZonePosition == gap));
                Check(player.BoardZone.GetSlot(gap) == depths && depths[GameTag.CARDTYPE] == 39, "left/middle/right play");
                Step(player.Options().OfType<PlayCardTask>().Single(t => t.Source == network && t.ZonePosition == gap));
                var victim = player.Opponent.BoardZone.FirstOrDefault();
                if (victim == null)
                {
                    victim = (Minion)Entity.FromCard(player.Opponent, Cards.FromId("CS2_231"), zone:player.Opponent.BoardZone);
                    game.DeathProcessingAndAuraUpdate(); session.Flush();
                }
                var activation = player.Options().OfType<ActivateLocationTask>().Single(t => t.Source == depths && t.ActionTarget == victim);
                Check(Simulation.TaskKey(activation).StartsWith($"9:{player.Id}:{depths.Id}:{victim.Id}:"), "numeric replay type/target");
                Check(Simulation.Legal(game).Last().PlayerTaskType == PlayerTaskType.ACTIVATE_LOCATION, "numeric legal ordering");
                var canonical = Canonical.State(game);
                depths.Location.Stage++;
                Check(Canonical.State(game) != canonical, "canonical stage missing"); depths.Location.Stage--;
                depths.Location.ConditionLocked = true;
                Check(Canonical.State(game) != canonical, "canonical lock missing"); depths.Location.ConditionLocked = false;
                depths.Location.UsesCompleted++;
                Check(Canonical.State(game) != canonical, "canonical uses missing"); depths.Location.UsesCompleted--;
                depths[GameTag.DAMAGE]++;
                Check(Canonical.State(game) != canonical, "canonical spent durability missing"); depths[GameTag.DAMAGE]--;
                Step(activation);
                Check(depths.Durability == 2 && session.Client.Entities[depths.Id].Get(GameTag.HEALTH) == 3
                    && session.Client.Entities[depths.Id].Get(GameTag.DAMAGE) == 1, "DurabilityDamageEncoding");
                Check(depths.IsExhausted && depths[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "closed");
                Step(player.Options().OfType<ActivateLocationTask>().Single(t => t.Source == network));
                Check(player.BoardZone.GetSlot(network.ZonePosition + 1).Card.Id == "JAIL_877t", "Unpinned_LocationSummonPosition");
                Check(session.Client.Entities[depths.Id].CardId == depths.Card.Id
                    && session.Client.Entities[network.Id].CardId == network.Card.Id, "both perspectives visible on board");
                Step(EndTurnTask.Any(player));
                Check(depths.IsExhausted && depths[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "CooldownTraceTiming opponent stays closed");
                Step(EndTurnTask.Any(player.Opponent));
                Check(depths.IsExhausted && depths[GameTag.LOCATION_ACTION_COOLDOWN] == 0 &&
                    !player.Options().OfType<ActivateLocationTask>().Any(), "CooldownSkipsOwnersNextTurn ajar");
                Step(EndTurnTask.Any(player));
                Check(depths.IsExhausted && depths[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "CooldownTraceTiming opponent stays ajar");
                Step(EndTurnTask.Any(player.Opponent));
                Check(!depths.IsExhausted && depths[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "CooldownSkipsOwnersNextTurn open on following owner turn");
                while (!player.BoardZone.IsFull)
                    Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_182")), -1, null);
                game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
                int former = network.ZonePosition, deaths = player.NumFriendlyMinionsThatDiedThisTurn;
                Step(player.Options().OfType<ActivateLocationTask>().Single(t => t.Source == network));
                Check(network.Zone.Type == Zone.GRAVEYARD && player.BoardZone.SlotCount == 7
                    && player.BoardZone.GetSlot(former).Card.Id == "JAIL_877t"
                    && player.NumFriendlyMinionsThatDiedThisTurn == deaths, "Unpinned_FullBoardSummonActivation");
                var wire = History(session).ToArray();
                int start = Array.FindLastIndex(wire, e => e.HasPowerStart && e.PowerStart.Source == network.Id);
                int retired = Array.FindIndex(wire, start, e => e.HasTagChange && e.TagChange.Entity == network.Id
                    && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)Zone.GRAVEYARD);
                int summoned = Array.FindIndex(wire, start, e => e.HasFullEntity && e.FullEntity.Name == "JAIL_877t");
                Check(start >= 0 && retired > start && summoned > retired, "summon final charge frees slot before recipe");
                depths.Destroy(); game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
                Check(depths.Zone.Type == Zone.GRAVEYARD, "location death");
                Step(EndTurnTask.Any(player));
            }
            CheckWire(session);
        }
        StageAndRenovator();
        VolcanoActivation();
        LocationReplay();
        Console.WriteLine("PASS locations transport: seeded gaps; Depths/Network; both perspectives; closed/ajar/open; final full-board charge; death; canonical/replay; reveal order; balanced blocks; no ghosts");
    }
    // Direct fixture for Erupting Volcano (CATA_584): the strict corpus plays random legal actions and nothing guarantees it reaches an
    // activation, so the missile sub-spell shape (one per hit, 3 and 6 after a Fire spell, final charge, hits that kill) goes through the
    // real packet client and invariants here, both seats.
    private static void VolcanoActivation()
    {
        foreach (int seed in new[] { 5, 9 })
        foreach (bool fire in new[] { false, true })
        foreach (bool final in new[] { false, true })
        foreach (bool secondSeat in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = secondSeat ? 2 : 1, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush();
            var player = game.CurrentPlayer; player.BaseMana = 10; player.UsedMana = 0;
            for (int n = 0; n < 2; n++)
            {
                var victim = (Minion)Entity.FromCard(player.Opponent, Cards.FromId("CS2_168"), zone: player.Opponent.BoardZone);   // 2/1: a hit kills it
            }
            var volcano = (BoardObject)Generic.DrawCard(player, Cards.FromId("CATA_584"));
            var fireball = fire ? Generic.DrawCard(player, Cards.FromId("CORE_CS2_029")) : null;
            game.DeathProcessingAndAuraUpdate(); session.Flush();
            void Step(PlayerTask task)
            {
                session.PublishDecision(task.Controller);
                session.Process(task);
                var violations = Invariants.Compare(game, session.Client).ToArray();
                Check(violations.Length == 0, "Volcano " + string.Join("; ", violations.Select(v => v.ToString())));
            }
            Step(player.Options().OfType<PlayCardTask>().Single(t => t.Source == volcano));
            if (fire) Step(player.Options().OfType<PlayCardTask>().First(t => t.Source == fireball && t.Target == player.Opponent.Hero));
            if (final) volcano[GameTag.DAMAGE] = volcano[GameTag.HEALTH] - 1;
            int before = History(session).Count();
            Step(player.Options().OfType<ActivateLocationTask>().Single(t => t.Source == volcano));
            var wire = History(session).Skip(before).ToArray();
            int start = Array.FindIndex(wire, e => e.HasPowerStart && e.PowerStart.Source == volcano.Id && (int)e.PowerStart.Type == (int)BlockType.POWER);
            Check(start >= 0, "Volcano POWER block on the wire");
            var body = wire.Skip(start).ToArray();
            int hits = fire ? 6 : 3, damage = body.Count(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE);
            int shots = body.Count(e => e.HasSubSpellStart && e.SubSpellStart.SourceEntityId == volcano.Id);
            Check(damage == hits && shots == hits && body.Count(e => e.HasSubSpellEnd) == shots,
                $"Volcano fire={fire} final={final}: {damage} hits, {shots} missiles from the location");
            Check(final == (volcano.Zone.Type == Zone.GRAVEYARD), "Volcano final charge retires the location");
            CheckWire(session);
        }
    }
    private static void StageAndRenovator()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); var p = game.Player1; p.BaseMana = 10;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush();
            void Observe()
            {
                var failures = Invariants.Compare(game, session.Client).ToArray();
                Check(failures.Length == 0, string.Join("; ", failures.Select(v => v.ToString())));
            }
            void Step(PlayerTask task)
            {
                session.PublishDecision(task.Controller); session.Process(task); Observe();
            }
            var left = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone:p.BoardZone);
            var right = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone:p.BoardZone);
            var location = Generic.DrawCard(p, Cards.FromId("TIME_044"));
            Generic.AddEnchantmentBlock(game, Cards.FromId("BRM_028e"), p.Hero, location);
            game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
            Check(location.Cost == 1, "Gnomeregan discounted in hand");
            int manaBeforePlay = p.RemainingMana;
            Step(p.Options().OfType<PlayCardTask>().Single(t => t.Source == location && t.ZonePosition == 1));
            Check(p.RemainingMana == manaBeforePlay - 1 &&
                session.Client.Entities[location.Id].Get(GameTag.TAG_LAST_KNOWN_COST_IN_HAND) == 1,
                "Gnomeregan hand play preserves discounted paid cost");
            var source = (BoardObject)location;
            for (int stage = 1; stage <= 2; stage++)
            {
                Step(p.Options().OfType<ActivateLocationTask>().Single(t => t.Source == source && t.ActionTarget == left));
                var visible = session.Client.Entities[source.Id];
                Check(visible.Get(GameTag.TAG_LAST_KNOWN_COST_IN_HAND) == 1,
                    "Gnomeregan activation preserves discounted paid cost");
                Check(visible.CardId == "TIME_044t" + stage && visible.Get(GameTag.ZONE_POSITION) == 2 &&
                    visible.Get(GameTag.HEALTH) == 3 && visible.Get(GameTag.DAMAGE) == stage && visible.Get(GameTag.EXHAUSTED) == 1 &&
                    visible.Get(GameTag.LOCATION_ACTION_COOLDOWN) == 1 && source.Id == location.Id && source.ZonePosition == 1,
                    "Gnomeregan CHANGE_ENTITY keeps id/position/HEALTH/DAMAGE/exhaustion/2353");
                var change = History(session).Last(e => e.HasChangeEntity && e.ChangeEntity.Entity == source.Id);
                Check(change.ChangeEntity.Name == source.Card.Id && change.ChangeEntity.Tags.Any(t => t.Name == (int)GameTag.HEALTH && t.Value == 3)
                    && change.ChangeEntity.Tags.Any(t => t.Name == (int)GameTag.DAMAGE && t.Value == stage - 1),
                    "stage packet keeps pre-debit DAMAGE and printed HEALTH");
                Step(EndTurnTask.Any(p)); Step(EndTurnTask.Any(p.Opponent));
                Check(!p.Options().OfType<ActivateLocationTask>().Any(t => t.Source == source), "stage stays unusable on next owner turn");
                Step(EndTurnTask.Any(p)); Step(EndTurnTask.Any(p.Opponent));
            }
            Step(p.Options().OfType<ActivateLocationTask>().Single(t => t.Source == source && t.ActionTarget == right));
            Check(session.Client.Entities[source.Id].Get(GameTag.TAG_LAST_KNOWN_COST_IN_HAND) == 1,
                "Gnomeregan final activation preserves discounted paid cost");
            Check(source.Zone.Type == Zone.GRAVEYARD && right.HasDivineShield, "Future final charge wire retirement");
            p.UsedMana = 0;
            var target = (BoardObject)Entity.FromCard(p.Opponent, Cards.FromId("BE_251"), zone:p.Opponent.BoardZone);
            var renovator = Generic.DrawCard(p, Cards.FromId("CORE_REV_023"));
            game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
            var play = p.Options().OfType<PlayCardTask>().First(t => t.Source == renovator && t.ActionTarget == target);
            Step(play);
            Check(target.Zone.Type == Zone.GRAVEYARD && session.Client.Entities[target.Id].Get(GameTag.ZONE) == (int)Zone.GRAVEYARD && game.NumMinionsKilledThisTurn == 0,
                "Renovator location Battlecry target reaches transport death path");
            CheckWire(session);
        }
        Console.WriteLine("PASS Gnomeregan stage CHANGE_ENTITY id/position/HEALTH/EXHAUSTED/2353 and Renovator location targeting transport seeds 1-3");
    }
    private static void LocationReplay()
    {
        var runner = new Runner();
        string deck = Simulation.DeckIds("standard-batch-1").Last();
        for (int seed = 0; seed < 10; seed++)
        {
            var replay = new TransportReplay { Game = new GameRecord { Id = $"locations-replay-{seed}",
                Suite = "standard-batch-1", Player = deck, Opponent = deck, Seed = seed } };
            int before = runner.LocationActivations;
            runner.Run(replay);
            if (runner.LocationActivations == before) continue;
            string findings = replay.FindingsSha;
            runner.Run(replay, verify:true);
            Check(runner.ViolationCount == 0 && replay.FindingsSha == findings, "activation replay changed legal keys or native findings");
            return;
        }
        Check(false, "seeded replay never activated a location");
    }
    private static IEnumerable<PowerHistoryData> History(NativeSession session)
    {
        foreach (var packet in session.Packets.Where(p => p.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(packet.Base64));
            var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
            foreach (var entry in history.List) yield return entry;
        }
    }
    private static void CheckWire(NativeSession session)
    {
        var known = new HashSet<int>(); var shown = new HashSet<int>(); int depth = 0;
        foreach (var entry in History(session))
        {
            if (entry.HasCreateGame)
            {
                known.Add(entry.CreateGame.GameEntity.Id); shown.Add(entry.CreateGame.GameEntity.Id);
                foreach (var player in entry.CreateGame.Players) { known.Add(player.Entity.Id); shown.Add(player.Entity.Id); }
            }
            if (entry.HasFullEntity || entry.HasShowEntity)
            {
                var entity = entry.HasFullEntity ? entry.FullEntity : entry.ShowEntity;
                if (entry.HasShowEntity) Check(known.Contains(entity.Entity), "show before full entity");
                known.Add(entity.Entity);
                if (!string.IsNullOrEmpty(entity.Name)) shown.Add(entity.Entity);
            }
            if (entry.HasTagChange) Check(known.Contains(entry.TagChange.Entity), "tag reference before entity");
            if (entry.HasPowerStart)
            {
                Check(entry.PowerStart.Source == 0 || shown.Contains(entry.PowerStart.Source), $"show before POWER/PLAY source reference {entry.PowerStart.Source}, type {entry.PowerStart.Type}");
                Check(entry.PowerStart.Target == 0 || shown.Contains(entry.PowerStart.Target), "show before target reference");
                depth++;
            }
            if (entry.HasPowerEnd) { depth--; Check(depth >= 0, "unmatched block end"); }
        }
        Check(depth == 0, "unclosed native block");
    }
}
