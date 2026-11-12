using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineGame = SabberStoneCore.Model.Game;

namespace TransportInvariants;

internal static class AiFrontierProof
{
    internal static void Run()
    {
        var rows = File.ReadLines("tests/fixtures/playtest/ai-frontier-turn10.jsonl")
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        var header = rows[0];
        LocalDeck Deck(string name)
        {
            var row = header.GetProperty(name);
            var boards = row.GetProperty("sideboards").EnumerateObject().Select(p => new LocalSideboard {
                Source = p.Name, Cards = p.Value.EnumerateArray().Select(c => c.GetString()).ToList() });
            return new LocalDeck(name, row.GetProperty("hero").GetString(),
                row.GetProperty("cards").EnumerateArray().Select(c => c.GetString()).ToArray()).WithSideboards(boards);
        }
        var human = Deck("humanDeck"); var npc = Deck("npcDeck");
        var h1 = Cards.FromId(human.Hero); var h2 = Cards.FromId(npc.Hero);
        var game = new EngineGame(new GameConfig { Player1Name = "Player", Player2Name = "fixture",
            Player1HeroClass = h1.Class, Player1HeroCard = h1, Player1Deck = human.Build(),
            Player2HeroClass = h2.Class, Player2HeroCard = h2, Player2Deck = npc.Build(),
            Player1Sideboards = human.Sideboards, Player2Sideboards = npc.Sideboards,
            RandomSeed = header.GetProperty("engineSeed").GetInt32(), FormatType = FormatType.FT_WILD,
            StartPlayer = 1, Shuffle = true, SkipMulligan = false, History = true, EnableCorpses = true, Logging = false,
            ReplayPools = HsSim.PoolSnapshots.ForReplay(header.GetProperty("poolHash").GetString()) });
        game.StartGame();
        foreach (var row in rows.Skip(1))
        {
            string kind = row.GetProperty("type").GetString();
            if (kind == "main-begin") { if (game.Step == Step.BEGIN_MULLIGAN) game.MainBegin(); continue; }
            if (game.Step == Step.BEGIN_MULLIGAN && game.Player1.MulliganState == Mulligan.DONE && game.Player2.MulliganState == Mulligan.DONE)
                game.MainBegin();
            var owner = row.GetProperty("seat").GetInt32() == 1 ? game.Player1 : game.Player2;
            string action = row.TryGetProperty("action", out var a) ? a.GetString() : null;
            var pending = game.GetPendingDecisions().SingleOrDefault(d => d.ControllerId == owner.Id);
            if (pending != null && (kind == "mulligan" || action.StartsWith("CHOOSE:", StringComparison.Ordinal)))
            {
                int[] picks = row.GetProperty(kind == "mulligan" ? "keeps" : "choices").EnumerateArray().Select(p => p.GetInt32()).ToArray();
                SelfChecks.Check(game.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, picks), out string error),
                    "recorded fixture choice succeeds: " + error);
            }
            else
            {
                PlayerTask task = owner.Options().Single(t => BotAction.From(t).ToString() == action);
                SelfChecks.Check(game.Process(task), "recorded fixture action succeeds: " + action);
            }
        }
        SelfChecks.Check(game.Turn == 10 && game.CurrentPlayer == game.Player2 &&
            game.Player2.BoardZone.Select(m => m.Id).SequenceEqual(new[] { 74, 99, 100 }) &&
            game.Player2.Options().Count(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.ActionTarget?.Id == 4) == 3,
            "recorded turn has three legal face attacks");
        var budget = new Budget(280000, 70000, 4000, 14, 9, 160, 25, false, 45, 55);
        var personality = new Personality(0, 0, 0, 0, null, .5, 0, 0);
        var profile = new NpcProfile("fixture", 1, "fixture", "npc-pure-paladin", null, 21, personality, budget, "pure-paladin.v1");
        var plan = DeckPlans.ForId(profile.PlanId);
        var pilot = new GoldenPilot(profile, plan);
        var view = pilot.Options.FairViewFactory(game, 2, 10435, DeckStyle.Value);
        var job = TurnPlanner.Job(view, profile, plan, 10435, 30502, pilot.PublicInputs.OpponentPlayed, pilot.PublicInputs);
        while (!job.IsDone) job.StepNode();
        var line = job.Result.Line;
        SelfChecks.Check(job.Result.Nodes <= 30502 && line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == 65)
            && new[] { 74, 99, 100 }.All(id => line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == id && a.Target == 4)),
            "bounded planner retains free attacks: " + string.Join(";", line));
        SelfChecks.Check(line.Last().Type == PlayerTaskType.END_TURN && job.Result.Value > 132.797411067087,
            "complete improved plan scores above observed early end");
        Console.WriteLine("PASS AiFrontier: bounded live fixture retains Nozdormu and all three legal attacks");
    }
}
