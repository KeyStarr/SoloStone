using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class HistoryOriginProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "history origin: " + message);
    internal static void Run()
    {
        int startReads=0;
        HistoryFactsProof.Fixture(beforeStart:game=>game.TriggerManager.GameStartTrigger += _=>{
            Check(game.Player1.DeckZone.All(e=>game.HistoryFacts.Query.StartedInDeck(e.Id,game.Player1.Id)),
                "initial membership visible inside actual game-start trigger");startReads++;
        });
        Check(startReads==1,"game-start membership snapshot once");
        foreach(bool history in new[]{false,true})
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1; var q = g.HistoryFacts.Query;
            var original = p.DeckZone[0]; var generated = Entity.FromCard(p, original.Card);
            Check(q.StartedInDeck(original.Id,p.Id) && !q.StartedInDeck(generated.Id,p.Id), "physical original, generated identical dbf");
            Generic.ChangeEntityBlock(p, original, Cards.FromId("CS2_231"), false);
            Check(q.StartedInDeck(original.Id,p.Id), "in-place ChangeEntity keeps membership");
            Generic.ChangeEntityBlock(p, original, Cards.FromId("GAME_005"), false);
            Check(q.StartedInDeck(original.Id,p.Id), "same-ID CLR replacement keeps membership");
            original = g.IdEntityDic[original.Id];
            p.DeckZone.Remove(original); Generic.ShuffleIntoDeck(p,p.Hero,original);
            Check(q.StartedInDeck(original.Id,p.Id), "enrolled original shuffled back keeps membership");
            Generic.ShuffleIntoDeck(p,p.Hero,generated);
            Check(!q.StartedInDeck(generated.Id,p.Id), "foreign shuffled-in remains generated");
            var body = (Minion)p.DeckZone.First(e=>e is Minion && q.StartedInDeck(e.Id,p.Id));
            Generic.Draw(p,body); PlayEntity(g,p,body);
            Check(q.Plays().Last().StartedInDeck,"play freezes enrolled origin");
            var originalCopy=Generic.Copy(p,p.Hero,body,Zone.SETASIDE);
            Check(!q.StartedInDeck(originalCopy.Id,p.Id),"copy of enrolled original never inherits membership");
            Generic.TransformBlock(p,Cards.FromId("CS2_231"),body);
            var transformed = p.BoardZone.Single();
            Check(Generic.ReturnToHandBlock(p,transformed) && q.LatestHandEntry(transformed.Id).EntryKind==HistoryHandEntryKind.Other,
                "transformed body return is Other, not generation or draw");
            Check(transformed.Id != body.Id && !q.StartedInDeck(transformed.Id,p.Id), "TransformBlock creates foreign entity");
            var copy = Generic.Copy(p, p.Hero, transformed, Zone.SETASIDE);
            Check(!q.StartedInDeck(copy.Id,p.Id), "copy never inherits membership");
            var mulligan = HistoryFactsProof.Fixture(mulligan:true);
            var selected = mulligan.Player1.HandZone[0];
            Check(mulligan.HistoryFacts.Query.StartedInDeck(selected.Id,mulligan.Player1.Id), "opening draw already enrolled");
            Check(mulligan.Process(ChooseTask.Mulligan(mulligan.Player1,mulligan.Player1.HandZone.Where(e=>e.Id!=selected.Id).Select(e=>e.Id).ToList())), "mulligan accepted");
            Check(mulligan.HistoryFacts.Query.StartedInDeck(selected.Id,mulligan.Player1.Id)
                && selected.Zone==mulligan.Player1.DeckZone
                && !mulligan.HistoryFacts.Query.Shuffles().Any(), "mulligan retains membership, not gameplay shuffle");
            var controlGame=HistoryFactsProof.Fixture();var minion=(Minion)Play(controlGame,controlGame.Player1,"CS2_231");
            var playId=controlGame.HistoryFacts.Query.Plays().Single().PlayId;
            new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET)
                .Process(controlGame,controlGame.Player2,controlGame.Player2.Hero,minion);
            Check(controlGame.HistoryFacts.Query.Summons().Count()==1,"control transfer is not a summon");
            Kill(controlGame,minion);
            Check(controlGame.HistoryFacts.Query.Deaths().Single().FromPlayId==playId,"control transfer preserves played-turn ancestry");
            var verify=Game.LoadSnapshot(g.SaveSnapshot());
            Check(verify.HistoryFacts.CanonicalText()==g.HistoryFacts.CanonicalText(),"origin policies survive snapshot round trip");
            EventTimeOrigins(history);
        }
        Console.WriteLine("PASS history origin: physical membership, mulligan, form/CLR change, transform/copy/shuffled-in");
    }
    private static void EventTimeOrigins(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;
        var original=p.DeckZone[0];var generated=Entity.FromCard(p,original.Card);
        Generic.Draw(p,original);p.HandZone.Add(generated);
        PlayEntity(g,p,original);PlayEntity(g,p,generated);
        var plays=g.HistoryFacts.Query.Plays().ToArray();
        Check(plays.Length==2 && plays[0].DbfId==plays[1].DbfId && plays[0].StartedInDeck && !plays[1].StartedInDeck,
            "same-dbf original/generated plays freeze distinct physical membership");
        Kill(g,(Minion)original);Kill(g,(Minion)generated);
        var deaths=g.HistoryFacts.Query.Deaths().ToArray();
        Check(deaths.Length==2 && deaths[0].StartedInDeck && !deaths[1].StartedInDeck,
            "same-dbf original/generated deaths freeze distinct physical membership");
        Check(!g.HistoryFacts.Query.StartedInDeck(original.Id,p.Opponent.Id),"membership belongs to original owner");
    }
}
