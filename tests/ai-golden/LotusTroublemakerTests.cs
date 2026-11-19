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
                Check(decision.Trace.Kind == "search" && decision.Task.PlayerTaskType != PlayerTaskType.END_TURN,
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
}
