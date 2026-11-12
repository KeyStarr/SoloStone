using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class Program
{
    private static void B16RetainedMapPrivacy()
    {
        foreach (bool opponent in new[] { false, true }) {
            var g = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 17, StartPlayer = 1,
                Shuffle = false, SkipMulligan = true, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            g.StartGame();
            if (opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "privacy opponent turn");
            var p = g.CurrentPlayer; p.BaseMana = 10;
            foreach (var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
            foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
            foreach (string id in new[] { "CS2_182", "CS2_200", "CS2_231" })
                SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            void Answer(SabberStoneCore.Model.Game game, int selected) {
                var d = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { selected }), out var error), "privacy pick: " + error);
            }
            Check(g.Process(PlayCardTask.Any(p, Generic.DrawCard(p, Cards.FromId("TLC_515")))), "privacy Map play");
            int[] first = p.Choice.Choices.ToArray();
            Answer(g, first[0]);
            Check(g.Process(PlayCardTask.Any(p, p.HandZone.Single())), "privacy watched card play");
            int[] retained = p.Choice.Choices.ToArray();
            Check(retained.Length == 2, "privacy retained pair exists");
            foreach (var next in new[] { g.CloneExact(history: true), SabberStoneCore.Model.Game.LoadSnapshot(g.SaveSnapshot()) }) {
                var owner = next.CurrentPlayer;
                void Private(string stage) {
                    var before = next.SaveSnapshot();
                    var fair = FairBotState.Create(next, owner.Opponent.PlayerId, 83, DeckStyle.Value);
                    var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
                    FairViewHiddenOfferProof.AssertHiddenActors(next, view, owner.PlayerId, first.Concat(retained).ToArray());
                    Check(before.SequenceEqual(next.SaveSnapshot()), "privacy view leaves authoritative state unchanged");
                }
                Private("during follow-up");
                Answer(next, retained[0]);
                Private("after follow-up");
                Check(next.Process(EndTurnTask.Any(owner)), "privacy next turn");
                Private("next turn");
            }
        }
    }
}
