extern alias TransportFixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static partial class Program
{
    // #80: the native client marks an opposing card burned from deck to graveyard as "being drawn by the opponent" for good and
    // waits on it whenever the card is moved, shown or reset again, so the GAME_RESET never ran and input stayed off. A Rewind
    // after an opposing burn must leave that card in GRAVEYARD.
    private static IReadOnlyList<string> RewindMillFaults(Capture capture) =>
        TransportFixtures::TransportInvariants.BoardActorInvariant.Validate(capture.HistorySnapshots());

    private static int BurnedCards(Capture capture, int cursor = 0) =>
        capture.HistorySnapshots(cursor).Sum(h => h.List.Count(r => r.HasMetaData && r.MetaData.Type == HistoryMeta.Type.BURNED_CARD));

    private static void RewindAfterOpposingBurn()
    {
        foreach (string source in new[] { "TIME_000", "TIME_003", "TIME_034", "END_000p" })
        {
            string name = "rewind after opposing burn " + source;
            var capture = new Capture();
            bool power = source == "END_000p";
            var scenario = DebugScenario.Parse("humanSeat=first\noverdraw=npc-godfrey\nhumanMana=10" + (power ? "" : "\nhumanHand=" + source));
            var match = new NativeMatch(capture.Send, "mage", "mage", 8427) { DebugScenario = scenario, StartingPlayer = scenario.FirstPlayer };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game;
            // Turn 1 ends; on turn 2 the NPC draws into a full hand and Godfrey's Atlas burns the overflow to its graveyard.
            var end = capture.Last<AllOptions>(14);
            match.SendOption(end.Id, end.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
            Check(BurnedCards(capture) > 0, name + ": the NPC burned a card before the Rewind");
            Check(game.CurrentPlayer == game.Player1, name + ": the human is on turn");
            var options = capture.Last<AllOptions>(14);
            int cursor = capture.WirePackets.Count;
            int play;
            if (power)
            {
                game.Player1.Hero.Card = Cards.FromId("HERO_03"); // a Rogue: Blessing of the Bronze is the Imbued hero power
                ImbueRules.Imbue(game.Player1, game.Player1.Hero);
                FlushFieldHistory(match);
                typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(match, null);
                options = capture.Last<AllOptions>(14);
                var hp = game.Player1.Hero.HeroPower;
                Check(hp.Card.Id == "END_000p", name + ": Blessing of the Bronze is the hero power: " + hp.Card.Id);
                play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(hp.Id));
                Check(play >= 0, name + ": hero power option offered");
                match.SendOption(options.Id, play, 0, -1, 0);
            }
            else
            {
                var card = game.Player1.HandZone.First(c => c.Card.Id == source);
                play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(card.Id));
                Check(play >= 0, name + ": play option offered");
                match.SendOption(options.Id, play, 0, -1, card.Card.Type == CardType.MINION ? 1 : 0);
            }
            Check(game.Rewind != null && game.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND, name + ": Rewind offered");
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int> { game.Rewind.RewindEntityId });
            Check(game.Rewind == null && game.Player1.Choice == null, name + ": Rewind settled");
            var faults = RewindMillFaults(capture);
            Check(faults.Count == 0, name + ": the burned opposing card never moves again: " + string.Join("; ", faults.Take(3)));
            var after = capture.Last<AllOptions>(14);
            Check(after.Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN), name + ": End Turn offered after Rewind");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Rewind after an opposing burn: the burned card stays in GRAVEYARD, End Turn offered (#80)");
    }

    // An opposing card burned DURING the attempt (Loot Hoarder dies to Chrono Daggers while the NPC's hand is full) is restored
    // to the deck by the reset and may be shown again by the replay. The client holds it milled, so the reset must give such a
    // card a fresh private actor and keep the burned one in GRAVEYARD.
    private static void RewindAfterAttemptBurn()
    {
        int burning = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            string name = "rewind after an attempt burn, seed " + seed;
            var capture = new Capture();
            var scenario = DebugScenario.Parse("humanSeat=first\noverdraw=npc\nnpcBoard=CORE_EX1_096\nhumanHand=TIME_001\nhumanMana=10");
            var match = new NativeMatch(capture.Send, "mage", "mage", 8427) { DebugScenario = scenario, StartingPlayer = scenario.FirstPlayer };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game;
            game.SetRandomSeed(seed);
            var card = game.Player1.HandZone.First(c => c.Card.Id == "TIME_001");
            var options = capture.Last<AllOptions>(14);
            int play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(card.Id));
            Check(play >= 0, name + ": play option offered");
            int cursor = capture.WirePackets.Count;
            match.SendOption(options.Id, play, 0, -1, 0);
            Check(game.Rewind != null && game.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND, name + ": Rewind offered");
            int burned = BurnedCards(capture, cursor);
            if (burned > 0) burning++;
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int> { game.Rewind.RewindEntityId });
            Check(game.Rewind == null && game.Player1.Choice == null, name + ": Rewind settled");
            var faults = RewindMillFaults(capture);
            Check(faults.Count == 0, name + " (attempt burns " + burned + "): the burned opposing card never moves again: " + string.Join("; ", faults.Take(3)));
            var after = capture.Last<AllOptions>(14);
            Check(after.Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN), name + ": End Turn offered after Rewind");
            // The live audit (TestControlVisibleDiff) maps each named client actor to its engine card; it must still find every
            // actor that is not a ghost revealed, or it reports `unrevealed:` and holds canAct false.
            foreach (var (id, named, zone, controller) in TransportFixtures::TransportInvariants.BoardActorInvariant.ClientCards(capture.HistorySnapshots()))
            {
                if (zone != 1 && zone != 4 && !(zone == 3 && controller == 1) || match.IsGhostActor(id)) continue;
                try { match.SnapshotVisibleTransportTags(game.IdEntityDic[match.EngineReturnId(id)]); }
                catch (TransportEntityNotVisibleException) { Check(false, name + ": unrevealed:" + id + ":" + named + " zone=" + zone + " engine=" + game.IdEntityDic[match.EngineReturnId(id)].Zone?.Type + " wire=" + match.WireReturnId(match.EngineReturnId(id)) + " after the Rewind"); }
            }
            capture.SerializeAll();
        }
        Check(burning > 0, "at least one seed burns an opposing card during the attempt");
        Console.WriteLine("PASS Rewind after an opposing burn during the attempt: Loot Hoarder dies to Chrono Daggers, seeds 1-6 (#80)");
    }
}
