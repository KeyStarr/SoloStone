using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// #81: after the player played Lotus Troublemaker, the NPC ended every later turn without acting.
internal static class LotusTroublemakerTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void TestNpcKeepsPlayingAfterLotus()
    {
        foreach (int twoManaPlays in new[] { 0, 1, 3 })
        {
            var g = Fixture();
            // The human is player 2 here; the NPC (bot id 1) decides after the human's turn.
            Check(g.Process(EndTurnTask.Any(g.Player1)), "setup end turn");
            var human = g.Player2; human.BaseMana = 10; human.UsedMana = 0;
            // Drawn from the deck, as in a real match: only a play that started in the deck reaches the opponent's public plays.
            var lotus = Entity.FromCard(human, Cards.FromId("JAIL_470"), zone: human.DeckZone);
            SabberStoneCore.Actions.Generic.Draw(human, lotus);
            for (int i = 0; i < twoManaPlays; i++)
            {
                var croc = Entity.FromCard(human, Cards.FromId("CS2_120"), zone: human.HandZone);
                Check(g.Process(human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == croc.Id)), "two-Mana play");
            }
            human.UsedMana = 0;
            Check(g.Process(human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == lotus.Id)), "Lotus play");
            Check(g.Process(EndTurnTask.Any(human)), "human end turn");
            var bot = g.Player1; bot.BaseMana = 10; bot.UsedMana = 0;
            Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.HandZone);
            foreach (var profile in NpcCatalog.All.Take(3))
            {
                var pilot = new GoldenPilot(profile); int index = 0;
                var decision = BotDriver.Decide(g, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(g.Turn, index, 997 * g.Turn + index * 31, profile)), ref index, pilot.Options);
                // A broken run searches 18-24 nodes; a working one searches thousands.
                Check(decision.Trace.Kind == "search" && decision.Task.PlayerTaskType != PlayerTaskType.END_TURN && decision.Trace.PilotDecision.Nodes >= 200,
                    $"Lotus with {twoManaPlays} two-Mana plays: NPC {profile.Id} must still play, got {decision.Task} trace={PlannerTests.Trace(decision.Trace)}");
            }
        }
    }

    // Class sweep for #81: every card whose text reads the match history (the Lotus family) is drawn from the human's deck and played
    // from a Rogue seat (the report's class), then each golden NPC decides. A decision may end the turn when that is best, but it
    // must come from the search, never from the driver's fallback.
    public static void TestNpcSearchesAfterHistoryCards()
    {
        var cards = Cards.All.Where(c => c.Implemented && c.Collectible && c.ConsumesHistory
            && (c.Type == CardType.MINION || c.Type == CardType.SPELL || c.Type == CardType.WEAPON)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        int played = 0, skipped = 0;
        foreach (var card in cards)
        {
            var g = Fixture(CardClass.WARRIOR, CardClass.ROGUE);
            Check(g.Process(EndTurnTask.Any(g.Player1)), "setup end turn");
            var human = g.Player2; human.BaseMana = 10; human.UsedMana = 0;
            var subject = Entity.FromCard(human, card, zone: human.DeckZone);
            SabberStoneCore.Actions.Generic.Draw(human, subject);
            var play = human.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == subject.Id);
            if (play == null || !g.Process(play)) { skipped++; continue; }
            played++;
            for (int guard = 0; guard < 8; guard++)
            {
                var pending = g.GetPendingDecisions().FirstOrDefault(d => d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options
                    && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Target && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.ChooseOne);
                if (pending == null) break;
                // A Rewind offer keeps the play (the Keep card is the second option); any other decision takes its first option.
                var option = human.Choice?.ChoiceAction == ChoiceAction.REWIND ? pending.Options.Last() : pending.Options.First();
                Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pending.Id, pending.ControllerId, new[] { option.OptionId }), out var error), card.Id + ": answer " + error);
            }
            if (g.CurrentPlayer != human || !g.Process(EndTurnTask.Any(human))) { skipped++; continue; }
            var bot = g.Player1; bot.BaseMana = 10; bot.UsedMana = 0;
            Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.HandZone);
            foreach (var profile in NpcCatalog.All.Take(3))
            {
                var pilot = new GoldenPilot(profile); int index = 0;
                var decision = BotDriver.Decide(g, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(g.Turn, index, 997 * g.Turn + index * 31, profile)), ref index, pilot.Options);
                Check(decision.Trace.Kind == "search", $"after the human's {card.Id} ({card.Name}) {profile.Id} decided by {decision.Trace.Kind}: {decision.Trace.FallbackReason}");
            }
        }
        Console.WriteLine($"  history-card sweep: {cards.Count} cards, {played} played, {skipped} not playable bare");
        Check(played >= 5, "the sweep played history cards: " + played);
    }

    // The routes that leave a Lotus the NPC KNOWS in the human's hand (#81): played then returned to hand, or revealed.
    // Its history display needs private hand residence, which the NPC's fair view refuses; that must not stop the NPC acting.
    private static Game KnownLotusGame(string route, int twoManaPlays)
    {
        var g = Fixture();
        Check(g.Process(EndTurnTask.Any(g.Player1)), "setup end turn");
        var human = g.Player2; human.BaseMana = 10; human.UsedMana = 0;
        var lotus = Entity.FromCard(human, Cards.FromId("JAIL_470"), zone: route == "created-revealed" ? (SabberStoneCore.Model.Zones.IZone)human.HandZone : human.DeckZone);
        if (route != "created-revealed") SabberStoneCore.Actions.Generic.Draw(human, lotus);
        for (int i = 0; i < twoManaPlays; i++)
        {
            var croc = Entity.FromCard(human, Cards.FromId("CS2_120"), zone: human.HandZone);
            Check(g.Process(human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == croc.Id)), "two-Mana play");
        }
        human.UsedMana = 0;
        if (route == "bounced")
        {
            Check(g.Process(human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == lotus.Id)), "Lotus play");
            Check(SabberStoneCore.Actions.Generic.ReturnToHandBlock(human, (Minion)lotus), "bounce");
            Check(lotus.Zone == human.HandZone, "Lotus is back in the human's hand");
        }
        else lotus[GameTag.REVEALED] = 1;
        Check(g.Process(EndTurnTask.Any(human)), "human end turn");
        var bot = g.Player1; bot.BaseMana = 10; bot.UsedMana = 0;
        Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.HandZone);
        return g;
    }

    public static void TestNpcKeepsPlayingWithKnownLotusInHumanHand()
    {
        foreach (string route in new[] { "bounced", "deck-revealed", "created-revealed" })
        foreach (int twoManaPlays in new[] { 0, 3 })
        {
            var g = KnownLotusGame(route, twoManaPlays);
            foreach (var profile in NpcCatalog.All.Take(3))
            {
                var pilot = new GoldenPilot(profile); int index = 0;
                var decision = BotDriver.Decide(g, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(g.Turn, index, 997 * g.Turn + index * 31, profile)), ref index, pilot.Options);
                // A broken run searches 18-24 nodes; a working one searches thousands.
                Check(decision.Trace.Kind == "search" && decision.Task.PlayerTaskType != PlayerTaskType.END_TURN
                    && decision.Trace.PilotDecision.Nodes >= 200,
                    $"Lotus {route}, {twoManaPlays} two-Mana plays: NPC {profile.Id} must act (not END_TURN) after a real search, got {decision.Task} trace={PlannerTests.Trace(decision.Trace)}");
            }
        }
    }

    // The display recipes are private to the engine's Card; read them by reflection so the sweep covers every card that has one.
    private static bool HasDisplay(Card card, string field) =>
        typeof(Card).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(card) != null;

    // Every history-display card (hand or board display), a revealed copy in the human's hand, one playable minion for the NPC.
    public static void TestNpcActsWithAnyKnownHistoryDisplayCardInHumanHand()
    {
        var cards = Cards.All.Where(c => c.Implemented && c.ConsumesHistory && (HasDisplay(c, "_historyDisplays") || HasDisplay(c, "_boardHistoryDisplays")))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        Check(cards.Count >= 30, "history-display cards found: " + cards.Count);
        // The four cards that made the NPC pass before the fix (#81): JAIL_470 Lotus Troublemaker, BE_012 Dark Deacon,
        // CATA_131 Felwood Treant, CATA_140 Merithra of the Dream. The sweep must keep them.
        foreach (string id in new[] { "JAIL_470", "BE_012", "CATA_131", "CATA_140" })
            Check(cards.Any(c => c.Id == id), id + " is in the history-display sweep");
        KnownCardSweep(cards, 3, "history-display");
    }

    // The same class beyond displays: every implemented card that reads history (hand auras, costs, Lotus family), one profile each.
    public static void TestNpcActsWithAnyKnownHistoryCardInHumanHand()
    {
        var cards = Cards.All.Where(c => c.Implemented && c.ConsumesHistory && (c.Type == CardType.MINION || c.Type == CardType.SPELL || c.Type == CardType.WEAPON)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        Check(cards.Count >= 70, "history cards found: " + cards.Count);
        KnownCardSweep(cards, 1, "history-consuming");
    }

    private static void KnownCardSweep(System.Collections.Generic.List<Card> cards, int profiles, string label)
    {
        int stuck = 0; var names = new System.Collections.Generic.List<string>();
        foreach (var card in cards)
        {
            var g = Fixture(CardClass.WARRIOR, CardClass.ROGUE);
            Check(g.Process(EndTurnTask.Any(g.Player1)), "setup end turn");
            var human = g.Player2; human.BaseMana = 10; human.UsedMana = 0;
            var subject = Entity.FromCard(human, card, zone: human.HandZone);
            subject[GameTag.REVEALED] = 1;
            // A board display is also computed while the card stands on the human's board.
            if (HasDisplay(card, "_boardHistoryDisplays") && card.Type == CardType.MINION)
            {
                var onBoard = (Minion)Entity.FromCard(human, card);
                SabberStoneCore.Actions.Generic.SummonBlock(g, onBoard, -1, null);
                g.DeathProcessingAndAuraUpdate();
            }
            Check(g.Process(EndTurnTask.Any(human)), "human end turn");
            var bot = g.Player1; bot.BaseMana = 10; bot.UsedMana = 0;
            Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.HandZone);
            Entity.FromCard(bot, Cards.FromId("CS2_120"), zone: bot.HandZone);
            foreach (var profile in NpcCatalog.All.Take(profiles))
            {
                var pilot = new GoldenPilot(profile); int index = 0;
                var decision = BotDriver.Decide(g, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(g.Turn, index, 997 * g.Turn + index * 31, profile)), ref index, pilot.Options);
                if (decision.Task.PlayerTaskType == PlayerTaskType.END_TURN || decision.Trace.PilotDecision.Nodes < 200) { stuck++; names.Add(card.Id + "/" + profile.Id); }
            }
        }
        Console.WriteLine($"  known {label} sweep: {cards.Count} cards, {stuck} passes");
        Check(stuck == 0, "the NPC passed with a known " + label + " card in the human's hand: " + string.Join(", ", names));
    }

    // A decision whose every simulated action is discarded as private history was never searched; it must leave a fault line
    // (the pre-fix shape of #81 was a silent search END_TURN). Forced with the Phoenix ignition refusal, which throws the same
    // "Private historical ..." error on END_TURN.
    public static void TestAllDiscardedPrivateHistoryLeavesFaultLine()
    {
        var g = Fixture();
        var state = View(g);
        var faults = new System.Collections.Generic.List<string>();
        state.FaultReporter = faults.Add;
        var game = (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
        var fire = Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1, Cards.FromId("CATA_EVENT_001e"));
        fire[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
        bool refused = false;
        try { state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN)); } catch (Exception e) when (FairHistory.IsPrivate(e)) { refused = true; }
        Check(refused, "fixture must throw private history");
        state.ReportAllDiscarded();
        Check(faults.Count == 1 && faults[0].StartsWith("private-history-all-discarded discarded=1 error=Private historical", StringComparison.Ordinal),
            "all-discarded decision reports its cause: " + string.Join("|", faults));

        // Control: one action applied means the search ran; nothing to report.
        var quiet = View(Fixture()); var quietFaults = new System.Collections.Generic.List<string>(); quiet.FaultReporter = quietFaults.Add;
        quiet.Apply(quiet.Actions.First(a => a.Type == PlayerTaskType.END_TURN)); quiet.ReportAllDiscarded();
        Check(quietFaults.Count == 0, "a decision that applied actions reports no discard fault");
    }

    // The live NPC decides through GoldenPilot's decision job (NativeMatch.BeginNpcDecision), not BotDriver.Decide: the job must
    // report the all-discarded decision itself, once.
    public static void TestLiveGoldenJobReportsAllDiscardedPrivateHistory()
    {
        var g = Fixture(); g.Player1.BaseMana = 0; g.Player1.UsedMana = 0; // only END_TURN is legal
        var state = View(g);
        var faults = new System.Collections.Generic.List<string>();
        state.FaultReporter = faults.Add;
        var game = (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
        var fire = Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1, Cards.FromId("CATA_EVENT_001e"));
        fire[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
        var profile = NpcCatalog.All.First();
        var pilot = new GoldenPilot(profile);
        var decision = pilot.Decide(state, new GoldenContext(g.Turn, 0, 997 * g.Turn, profile));
        Check(faults.Count(f => f.StartsWith("private-history-all-discarded", StringComparison.Ordinal)) == 1,
            "the golden decision job reports an all-discarded decision once: [" + string.Join("|", faults) + "] decision=" + decision.Action);
    }
}
