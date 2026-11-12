using System;
using System.Linq;
using System.Threading.Tasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static ReplyTests;

public static class ProjectedResidenceTests
{
    public static void TestNewViewerCannotRecoverErasedDeckResidence()
    {
        var source = Fixture();
        var before = source.SaveSnapshot();
        var projected = source.CloneExact();
        projected.ProjectHistoryForFairView(1);
        projected.ProjectHistoryForFairView(2);
        bool refused = false;
        try { projected.HistoryFacts.Query.Residences(projected.Player2.DeckZone.First().Id, Zone.DECK).ToArray(); }
        catch (NotSupportedException error) { refused = error.Message.StartsWith("Private historical ", StringComparison.Ordinal); }
        Check(refused, "new viewer recovered private past residence");
        foreach (bool reload in new[] { false, true })
        {
            var copy = reload ? Game.LoadSnapshot(projected.SaveSnapshot()) : projected.CloneExact();
            var drawn = copy.Player2.DeckZone.First();
            Generic.Draw(copy.Player2, drawn);
            Check(drawn.Zone == copy.Player2.HandZone, "known draw must resolve after viewer change");
            var events = copy.HistoryFacts.Query.Events<ResidenceChangedFact>().ToArray();
            Check(events.Last(r => !r.Entered && r.Zone == Zone.DECK).EntityId == 0, "erased deck entry must not acquire an identified exit");
            Check(events.Last(r => r.Entered && r.Zone == Zone.HAND).EntityId == drawn.Id, "new visible hand entry must acquire identity");
            Check(copy.HistoryFacts.Query.Residences(drawn.Id, Zone.HAND).Any(), "new residence must be queryable");
            Game.LoadSnapshot(copy.SaveSnapshot());
        }
        Check(before.SequenceEqual(source.SaveSnapshot()), "projection/draw changed authoritative game");
    }
    public static void TestProjectedResidenceForksStayIndependentAcrossWorkers()
    {
        var source = Fixture();
        source.ProjectHistoryForFairView(1);
        source.ProjectHistoryForFairView(2);
        var original = source.SaveSnapshot();
        Func<Game, string> draw = game => {
            Generic.Draw(game.Player2, game.Player2.DeckZone.First());
            Generic.Draw(game.Player2, game.Player2.DeckZone.First());
            Game.LoadSnapshot(game.SaveSnapshot());
            return game.HistoryFacts.CanonicalText();
        };
        var serial = draw(source.CloneExact());
        var first = source.CloneExact(); var second = source.CloneExact();
        var tasks = new[] { Task.Run(() => draw(first)), Task.Run(() => draw(second)) };
        Task.WaitAll(tasks);
        Check(tasks.All(task => task.Result == serial), "parallel residence histories differ from serial");
        Check(original.SequenceEqual(source.SaveSnapshot()), "worker changed shared ancestor");
    }

    public static void TestKnownResidenceStillClosesWithIdentity()
    {
        var source = Fixture();
        var actor = source.Player1.HandZone.FirstOrDefault();
        if (actor == null) { Generic.Draw(source.Player1, source.Player1.DeckZone.First()); actor = source.Player1.HandZone.Last(); }
        source.ProjectHistoryForFairView(1);
        var copy = source.CloneExact();
        var held = copy.Player1.HandZone.First(c => c.Id == actor.Id);
        copy.Player1.HandZone.Remove(held); copy.Player1.SetasideZone.Add(held);
        Check(copy.HistoryFacts.Query.Events<ResidenceChangedFact>().Last().EntityId == held.Id,
            "known hand exit lost its identity");
        Check(copy.HistoryFacts.Query.Residences(held.Id, Zone.HAND).Last().ExitSequence.HasValue,
            "known residence stayed open");
        Game.LoadSnapshot(copy.SaveSnapshot());
    }

}
