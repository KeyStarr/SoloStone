using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
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
    private static readonly SuspendableTask B16EnemyDeck = StateTaskList.Suspendable("native/b16/enemy-deck",
        new DiscoverThenContinueTask(DiscoverEntitySource.EnemyDeck, DiscoverEntityAction.PutOnTop));
    [GroupProof("b16-discover-ext-native", ProofKind.Native)]
    private static void B16DiscoverExtNative()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "TLC_515", "TLC_464", "JAIL_319", "TIME_861", "enemy-deck" }) {
            var g = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_029"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_029"), 30).ToList() });
            g.StartGame();
            if (opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "native fixture opponent turn");
            var p = g.CurrentPlayer; p.BaseMana = 10;
            foreach (var owner in new[] { p, p.Opponent })
                foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, g);
            void Flush() => typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(match, new object[] { g.PowerHistory.Last });
            Flush(); g.PowerHistory.Last.Clear();
            var source = Generic.DrawCard(p, Cards.FromId(id == "enemy-deck" ? "CS2_200" : id));
            if (id == "enemy-deck") B16EnemyDeck.Process(g, p, source, null);
            else Check(g.Process(PlayCardTask.Any(p, source)), "native extension play " + id);
            Flush(); g.PowerHistory.Last.Clear();
            int[] offers = p.Choice?.Choices.ToArray() ?? Array.Empty<int>();
            if (p.Choice != null) {
                if (!opponent) {
                    typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(match, null);
                    var packet = capture.Last<EntityChoices>(17);
                    Check(packet.ChoiceType == 2 && packet.CountMin == 1 && packet.CountMax == 1
                        && packet.Entities.SequenceEqual(offers), "retail GENERAL decision shape");
                }
                var decision = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { offers[0] }), out var error), "native answer: " + error);
                Flush();
            }
            var rows = capture.Packets.Select(x => x.Message).OfType<PowerHistory>().SelectMany(x => x.List).ToArray();
            var actors = rows.SelectMany(x => new[] { x.FullEntity, x.ShowEntity, x.ChangeEntity }).Where(x => x != null).ToArray();
            if (id == "enemy-deck" && !opponent)
                Check(actors.Where(x => p.Opponent.DeckZone.Any(c => c.Id == x.Entity)).All(x => string.IsNullOrEmpty(x.Name)),
                    "enemy deck originals remain hidden despite private proxies");
            if (opponent) {
                var hidden = p.HandZone.Select(c => c.Id).Concat(offers).ToHashSet();
                Check(actors.Where(x => hidden.Contains(x.Entity)).All(x => string.IsNullOrEmpty(x.Name)), "opponent hidden cards and offers never reveal identities");
                Check(OpposingPromptsHidden(capture, p.PlayerId), "opponent prompt shows only card backs");
            }
            Check(rows.All(x => !x.HasTagChange || !DiscoverRealRules.IsInternalTag((GameTag)x.TagChange.Tag)), "internal tag changes filtered");
            Check(actors.All(x => x.Tags.All(t => !DiscoverRealRules.IsInternalTag((GameTag)t.Name))), "internal snapshot tags filtered");
        }
        Console.WriteLine("PASS Discover extensions native hidden identities and mandatory choices");
    }

}
