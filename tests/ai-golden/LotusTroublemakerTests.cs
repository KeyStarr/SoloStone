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
            var lotus = Entity.FromCard(human, Cards.FromId("JAIL_470"), zone: human.HandZone);
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
}
