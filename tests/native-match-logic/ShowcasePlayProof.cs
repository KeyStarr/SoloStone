using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class ShowcasePlayProof
{
    internal static void Run()
    {
        var players = LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("showcase-", StringComparison.Ordinal)).ToArray();
        var opponents = LocalDecks.TestOpponents.Where(deck => deck.Id.StartsWith("showcase-ai-", StringComparison.Ordinal)).ToArray();
        if (players.Length != 12 || opponents.Length != 3) throw new Exception("Showcase proof needs 12 x 3 decks.");
        int games = 0, wins = 0, maxTurn = 0, searchFallbacks = 0;
        Console.WriteLine("| Player deck | Hunter | Paladin | Warrior |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var player in players)
        {
            var results = new List<string>();
            foreach (var opponent in opponents)
            {
                int seed = 8427 + games;
                var game = new SabberStoneCore.Model.Game(new GameConfig {
                    Player1HeroClass = Cards.FromId(player.Hero).Class,
                    Player2HeroClass = Cards.FromId(opponent.Hero).Class,
                    Player1Deck = player.Build(), Player2Deck = opponent.Build(),
                    StartPlayer = 1, Shuffle = true, SkipMulligan = true, RandomSeed = seed,
                    History = false, Logging = false });
                game.StartGame();
                int actions = 0, decisionIndex = 0;
                while (game.State != State.COMPLETE && game.Turn <= 100 && actions++ < 2000)
                {
                    // Suspended prompts can belong to the other controller during an effect.
                    var pending = game.GetPendingDecisions().SingleOrDefault();
                    var owner = pending == null ? game.CurrentPlayer : game.ControllerById(pending.ControllerId);
                    var tasks = owner.Options();
                    PlayerTask task = LocalBot.RewindKeep(tasks);
                    var answer = game.GetSelectorAnswer();
                    if (task == null && answer != null)
                        task = ChooseTask.Pick(owner, answer.SelectedOptionIds[0]);
                    if (task == null)
                    {
                        // Match NativeMatch.ChooseBotTask: bounded fair search, then public-state fallback.
                        try
                        {
                            var style = owner == game.Player1 ? player.Style : opponent.Style;
                            var state = FairBotState.Create(game, owner.PlayerId, unchecked(997 * game.Turn + ++decisionIndex * 31), style);
                            task = BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock()).Action.Resolve(tasks);
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine($"Showcase search fallback: {player.Id}/{opponent.Id} seed={seed} turn={game.Turn}: {error}");
                            searchFallbacks++;
                            task = LocalBot.Choose(tasks);
                        }
                    }
                    if (task is ChooseTask choice)
                    {
                        var decision = game.GetPendingDecisions().Single(d => d.ControllerId == owner.Id);
                        if (!game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, choice.Choices), out var error))
                            throw new Exception("Showcase choice rejected: " + error);
                    }
                    else if (!game.Process(task)) throw new Exception("Showcase action rejected: " + task);
                }
                if (game.State != State.COMPLETE || (game.Player1.PlayState != PlayState.WON && game.Player2.PlayState != PlayState.WON))
                    throw new Exception($"Showcase did not finish with a winner: {player.Id}/{opponent.Id} seed={seed} turn={game.Turn} actions={actions}");
                bool won = game.Player1.PlayState == PlayState.WON;
                results.Add((won ? "W" : "L") + "/" + game.Turn);
                if (won) wins++;
                maxTurn = Math.Max(maxTurn, game.Turn);
                games++;
            }
            Console.WriteLine("| " + player.Name + " | " + string.Join(" | ", results) + " |");
        }
        // Fair search must model every showcase decision; a fallback means the greedy bot played instead.
        if (searchFallbacks != 0) throw new Exception($"Showcase search fell back {searchFallbacks} times.");
        Console.WriteLine($"PASS showcase headless: {games}/36 winners; player wins={wins}; max turn={maxTurn}; game exceptions=0; search fallbacks={searchFallbacks}; seeds=8427..8462");
    }
}
