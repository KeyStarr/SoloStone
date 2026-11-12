using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

// Live 2026-10-26 (batch-e verify, match 2): Oblivia played Winterspring Whelp from hand slot 3 to board slot 3; the
// engine emits no ZONE_POSITION for an unchanged slot number, ZoneChangeList.ProcessChanges cleared the actor position
// on the HAND -> PLAY transition, Zone.CardSortComparison sorted the Whelp first and AutoCorrectZones never repairs the
// opposing battlefield: test control froze on `player2:boardOrder` with matching entity tags.
internal static class BoardPositionProof
{
    internal static void Run()
    {
        int cases = 0;
        foreach (int actor in new[] { 1, 2 })
            foreach (int target in new[] { 2, 1 }) // zero-based board slot: append at the end, or insert in the middle
            {
                var game = new Game(new GameConfig { RandomSeed = 11, StartPlayer = actor, SkipMulligan = true,
                    History = true, Logging = false,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
                game.StartGame();
                var player = game.CurrentPlayer;
                player.BaseMana = 10;
                NativeSession session = null;
                var match = new NativeMatch((id, packet) => session.Receive(id, packet));
                typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
                session = new(match);
                foreach (var old in player.HandZone.ToArray()) player.SetasideZone.Add(old.Zone.Remove(old));
                session.Flush();
                for (int i = 0; i < 2; i++)
                {
                    var minion = Generic.DrawCard(player, Cards.FromId("CS2_231"));
                    session.Flush();
                    session.Process(PlayCardTask.Any(player, minion, zonePosition: i));
                }
                // Hand slot (one-based) == board slot (one-based): the move carries no engine position change.
                var hand = Enumerable.Range(0, target + 1).Select(_ => Generic.DrawCard(player, Cards.FromId("CS2_231"))).ToArray();
                session.Flush();
                var played = hand[target];
                SelfChecks.Check(played[GameTag.ZONE_POSITION] == target + 1, "fixture hand slot equals the board slot");
                session.Process(PlayCardTask.Any(player, played, zonePosition: target));
                var engine = player.BoardZone.Select(card => card.Id).ToArray();
                SelfChecks.Check(engine[target] == played.Id && played[GameTag.ZONE_POSITION] == target + 1, "fixture board slot");
                var shown = session.Client.BoardOrder(player.PlayerId);
                SelfChecks.Check(shown.SequenceEqual(engine),
                    $"same-slot HAND -> PLAY keeps the native actor order actor={actor} slot={target + 1}: engine={string.Join(',', engine)} shown={string.Join(',', shown)}");
                SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "same-slot play keeps strict parity");
                cases++;
            }
        foreach (int actor in new[] { 1, 2 })
        {
            // Colossal without a layout: its first appendage inserts at the body's slot inside the nested TRIGGER,
            // shifting the body right there.
            var bodies = Cards.All.Where(c => c.ColossalAppendageIds != null).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            var shifted = bodies.FirstOrDefault(c => NestedShift(actor, c.Id, existing: 1, "Colossal " + c.Id, target: false));
            SelfChecks.Check(shifted != null, "a Colossal shifts its body in the appendage TRIGGER");
            // Fire Elemental kills its left neighbour with its Battlecry; the death compacts it one slot left later.
            SelfChecks.Check(NestedShift(actor, "CS2_042", existing: 2, "Fire Elemental", target: true),
                "Battlecry death shifts the played minion after its PLAY move");
            StaleClientPosition(actor);
            cases += 3;
        }
        Console.WriteLine($"PASS board positions transport: same-slot HAND -> PLAY end/middle, Colossal appendage and Battlecry death shifts, human/opponent; cases={cases}; violations=0");
    }

    // Played to the slot it held in hand, with a later shift only in a nested block: the PLAY block itself must carry
    // the destination, since each nested block is a separate client task list (drawn after the move).
    private static bool NestedShift(int actor, string cardId, int existing, string label, bool target)
    {
        var (game, session, player, _) = Fixture(actor);
        for (int i = 0; i < existing; i++)
        {
            var minion = Generic.DrawCard(player, Cards.FromId("CS2_231"));
            session.Flush();
            session.Process(PlayCardTask.Any(player, minion, zonePosition: i));
        }
        for (int i = 0; i < existing; i++) Generic.DrawCard(player, Cards.FromId("CS2_231"));
        var played = Generic.DrawCard(player, Cards.FromId(cardId));
        session.Flush();
        SelfChecks.Check(played[GameTag.ZONE_POSITION] == existing + 1, label + " fixture hand slot equals the board slot");
        int start = session.Packets.Count;
        var victim = target ? player.BoardZone[existing - 1] : null;
        session.Process(PlayCardTask.Any(player, played, victim, zonePosition: existing));
        var entries = session.Packets.Skip(start).Where(p => p.Id == 19).SelectMany(p =>
        {
            var history = new PegasusGame.PowerHistory();
            using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
            history.Deserialize(stream);
            return history.List;
        }).ToList();
        int open = entries.FindIndex(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.PLAY && e.PowerStart.Source == played.Id);
        SelfChecks.Check(open >= 0, label + " PLAY block");
        int nested = entries.FindIndex(open + 1, e => e.HasPowerStart || e.HasPowerEnd);
        int zone = entries.FindIndex(open + 1, e => e.HasTagChange && e.TagChange.Entity == played.Id && e.TagChange.Tag == (int)GameTag.ZONE
            && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.PLAY);
        SelfChecks.Check(zone > open && zone < nested && entries[nested].HasPowerStart, label + " moves inside PLAY before a nested block");
        SelfChecks.Check(entries.Skip(zone + 1).Take(nested - zone - 1).Any(e => e.HasTagChange && e.TagChange.Entity == played.Id
            && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value > 0),
            $"{label} actor={actor}: the PLAY task list carries the destination position before the nested shift");
        var engine = player.BoardZone.Select(card => card.Id).ToArray();
        var shown = session.Client.BoardOrder(player.PlayerId);
        SelfChecks.Check(shown.SequenceEqual(engine), $"{label} actor={actor} final order: engine={string.Join(',', engine)} shown={string.Join(',', shown)}");
        SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), label + " keeps strict parity");
        return played[GameTag.ZONE_POSITION] != existing + 1;
    }

    // The appended destination is the engine's position at that point, not the client copy: presentation paths
    // (restored choice actors, deck-offer snapshots) can leave the client at 0 while the engine slot never changes.
    private static void StaleClientPosition(int actor)
    {
        var (game, session, player, match) = Fixture(actor);
        var minion = Generic.DrawCard(player, Cards.FromId("CS2_231"));
        session.Flush();
        session.Process(PlayCardTask.Any(player, minion, zonePosition: 0));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var client = (Dictionary<int, Dictionary<int, int>>)typeof(NativeMatch).GetField("clientTransportTags", flags).GetValue(match);
        var engine = (Dictionary<int, int>)typeof(NativeMatch).GetField("historicalPositions", flags).GetValue(match);
        SelfChecks.Check(engine[minion.Id] == 1, "engine position is tracked historically");
        client[minion.Id][(int)GameTag.ZONE_POSITION] = 0; // what a presentation-only snapshot leaves behind
        var packet = new PegasusGame.PowerHistory();
        var move = new PegasusGame.PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
            Entity = minion.Id, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.PLAY } };
        var entries = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(minion.Id, GameTag.ZONE, (int)SabberStoneCore.Enums.Zone.PLAY) };
        typeof(NativeMatch).GetMethod("AppendMovedBoardPosition", flags).Invoke(match, new object[] { packet, move, entries, 0 });
        SelfChecks.Check(packet.List.Count == 1 && packet.List[0].HasTagChange && packet.List[0].TagChange.Entity == minion.Id
            && packet.List[0].TagChange.Tag == (int)GameTag.ZONE_POSITION && packet.List[0].TagChange.Value == 1,
            $"actor={actor}: a stale client position still gets the engine destination");
    }

    private static (Game, NativeSession, Controller, NativeMatch) Fixture(int actor)
    {
        var game = new Game(new GameConfig { RandomSeed = 11, StartPlayer = actor, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        var player = game.CurrentPlayer;
        player.BaseMana = 10;
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        foreach (var old in player.HandZone.ToArray()) player.SetasideZone.Add(old.Zone.Remove(old));
        session.Flush();
        return (game, session, player, match);
    }
}
