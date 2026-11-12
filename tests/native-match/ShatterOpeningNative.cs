using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static partial class Program
{
    private sealed class ShatterOpeningKeeps : MulliganPolicy
    {
        internal int[] Recorded;
        public override IReadOnlyList<int> Keep(MulliganView view) => Recorded ?? view.Offered.Where(c => c.CardId != "GAME_005").Select(c => c.EntityId).ToArray();
    }

    private static void ShatterOpeningNative(string record = null)
    {
        var previousDecks = LocalDecks.ExternalDecks;
        var previousMulligan = NpcWorldHooks.MulliganFor;
        try
        {
            foreach (string family in new[] { "CATA_489", "CATA_134" })
            foreach (int first in new[] { 1, 2 })
            foreach (bool scenario in new[] { false, true })
            {
                var cards = Enumerable.Repeat("CS2_182", 30).ToArray();
                // Seed 183 deals this parent to both seats through the real shuffled deal.
                cards[29] = family;
                var human = new LocalDeck("Shatter human", "HERO_08", cards);
                var npc = new LocalDeck("npc-zee-shaman", "Shatter NPC", "HERO_08", "NPC", "test", DeckStyle.Value, Array.Empty<string>(), cards.ToArray());
                LocalDecks.ExternalDecks = id => id == "shatter-opening" ? human : previousDecks?.Invoke(id);
                NpcWorldHooks.MulliganFor = (_, __) => new ShatterOpeningKeeps();
                var capture = new Capture();
                var match = new NativeMatch(capture.Send, "shatter-opening", "npc-zee-shaman", 183) {
                    StartingPlayer = first, SynchronousNpc = false,
                    DebugScenario = scenario ? DebugScenario.Parse("humanOpeningHand=" + family + "\nnpcOpeningHand=" + family) : null
                };
                typeof(NativeMatch).GetField("opponentDeck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(match, npc);
                match.Start();
                try { AssertShatterOpening(match, capture, family, scenario ? "scenario" : "shuffle"); }
                finally { match.StopNpc(); }
            }
            if (record != null)
            {
                // Read private decks only from caller-owned evidence; never store a fixture in this repository.
                var rows = File.ReadLines(record).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => JsonDocument.Parse(line)).ToArray();
                try
                {
                    var header = rows[0].RootElement;
                    LocalDeck Deck(string key) { var d = header.GetProperty(key); return new LocalDeck("Replay", d.GetProperty("hero").GetString(),
                        d.GetProperty("cards").EnumerateArray().Select(c => c.GetString()).ToArray()); }
                    var human = Deck("humanDeck"); var rawNpc = Deck("npcDeck");
                    var npc = new LocalDeck("npc-zee-shaman", "Journal NPC", rawNpc.Hero, "NPC", "test", DeckStyle.Value, Array.Empty<string>(), rawNpc.CardIds.ToArray());
                    var botKeeps = rows.Select(d => d.RootElement).First(row => row.GetProperty("type").GetString() == "mulligan" && row.GetProperty("seat").GetInt32() == 2)
                        .GetProperty("keeps").EnumerateArray().Select(id => id.GetInt32()).ToArray();
                    LocalDecks.ExternalDecks = id => id == "shatter-opening" ? human : previousDecks?.Invoke(id);
                    NpcWorldHooks.MulliganFor = (_, __) => new ShatterOpeningKeeps { Recorded = botKeeps };
                    var capture = new Capture();
                    var match = new NativeMatch(capture.Send, "shatter-opening", "npc-zee-shaman", header.GetProperty("engineSeed").GetInt32()) {
                        StartingPlayer = header.GetProperty("firstPlayer").GetInt32(), SynchronousNpc = false
                    };
                    typeof(NativeMatch).GetField("opponentDeck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(match, npc);
                    match.Start();
                    try
                    {
                        Check(match.Game.Player1.HandZone.First().Id == 31 && match.Game.Player1.HandZone.First().Card.Id == "CATA_489", "journal exact parent entity 31 in first slot");
                        AssertShatterOpening(match, capture, "CATA_489", "external journal");
                    }
                    finally { match.StopNpc(); }
                }
                finally { foreach (var row in rows) row.Dispose(); }
            }
        }
        finally { LocalDecks.ExternalDecks = previousDecks; NpcWorldHooks.MulliganFor = previousMulligan; }
        Console.WriteLine("PASS Shatter native opening: both seats, shuffled kept-all CHOOSE, CATA_489/CATA_134 families, opening scenario and optional external journal");
    }

    private static void AssertShatterOpening(NativeMatch match, Capture capture, string id, string label)
    {
        var game = match.Game;
        var choice = capture.Last<EntityChoices>(17);
        var parents = game.Player1.HandZone.Where(c => c.Card.Id == id).ToArray();
        Check(parents.Length == 1 && parents[0][ShatterRules.AlreadyCombined] == 0 && choice.Entities.Contains(parents[0].Id), label + " real parent mulligan offer, first=" + match.FirstPlayer + " hand=" + string.Join(",", game.Player1.HandZone.Select(c => c.Card.Id)));
        int before = game.Player1.HandZone.Count;
        int cursor = capture.Packets.Count;
        match.SendChoices(choice.Id, choice.Entities.ToList());
        var family = Cards.FromId(id).Shatter;
        var halves = game.Player1.HandZone.Where(c => c.Card.Id == family.FirstHalfId || c.Card.Id == family.SecondHalfId).ToArray();
        Check(halves.Length == 2 && !game.Player1.HandZone.Any(c => c.Card.Id == id) &&
            game.Player1.HandZone.Count == before + 1 + (match.FirstPlayer == 1 ? 1 : 0), label + " split survives first action boundary");
        Check(game.Player1.HandZone.Any(c => c.Card.Id == "GAME_005") == (match.FirstPlayer == 2), label + " Coin survives");
        if (label == "scenario")
            Check(game.Player2.HandZone.Any(c => c.Card.Id == family.FirstHalfId) &&
                game.Player2.HandZone.Any(c => c.Card.Id == family.SecondHalfId), "NPC opening override also settles through native keep-all");
        var history = History(capture, cursor);
        foreach (var half in halves)
            Check(history.Any(row => row.HasFullEntity && row.FullEntity.Entity == half.Id && SnapshotName(row) == half.Card.Id), label + " owner half FULL reaches native wire");
        Check(history.Any(row => row.HasHideEntity && row.HideEntity.Entity == parents[0].Id), label + " parent retirement reaches native wire");
        capture.SerializeAll();
        if (label == "external journal")
            Console.WriteLine("PASS exact journal: parent " + parents[0].Id + " retired; halves " + string.Join(",", halves.Select(c => c.Id)) + "; human hand=" + game.Player1.HandZone.Count + "; firstPlayer=" + match.FirstPlayer);
    }
}
