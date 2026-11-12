using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HistoryOperationsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "history operations: " + message);
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            int callbacks = 0;
            TriggerManager.TriggerHandler observe = entity => {
                var fact = g.HistoryFacts.Query.Events<ShuffleOperationFact>().Last();
                Check(fact.EntityId == entity.Id && fact.DestinationControllerId == entity.Controller.Id,
                    "successful member visible inside same shuffle trigger");
                callbacks++;
            };
            g.TriggerManager.ShuffleIntoDeckTrigger += observe;
            new AddCardTo("CS2_231", EntityType.DECK, 3).Process(g, p, p.Hero, null);
            Check(g.HistoryFacts.Query.Shuffles().Count() == 1 && callbacks == 3, "multi-card AddCardTo one operation");
            var stack = new TaskStack { Playables = Enumerable.Range(0, 2).Select(_ => Entity.FromCard(p, Cards.FromId("CS2_231"))).ToList() };
            new AddStackTo(EntityType.DECK).Process(g, p, p.Hero, null, stack);
            Check(g.HistoryFacts.Query.Shuffles().Count() == 2, "AddStackTo one operation");
            var a = Summon(p, "CS2_231"); var b = Summon(p, "CS2_231");
            new MoveToDeck(EntityType.STACK).Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable> { a, b } });
            Check(g.HistoryFacts.Query.Shuffles().Count() == 3, "MoveToDeck one operation");
            new CopyTask(EntityType.TARGET,Zone.DECK,2).Process(g,p,p.Hero,a);
            Check(g.HistoryFacts.Query.Shuffles().Count()==4,"multi-card CopyTask one operation");
            a = Summon(p, "CS2_231"); b = Summon(p.Opponent, "CS2_231");
            Play(g, p, "EDR_232"); // Typhoon: one CustomTask invocation across destination decks.
            Check(g.HistoryFacts.Query.Shuffles().Count() == 5 && p.BoardZone.Count + p.Opponent.BoardZone.Count == 0,
                "Typhoon one producer operation");
            g.TriggerManager.ShuffleIntoDeckTrigger -= observe;
            bool nested = false;
            TriggerManager.TriggerHandler nest = _ => {
                if (nested) return;
                nested = true;
                Generic.ShuffleIntoDeck(p.Opponent, p.Hero, Entity.FromCard(p.Opponent, Cards.FromId("CS2_231")));
            };
            g.TriggerManager.ShuffleIntoDeckTrigger += nest;
            new AddCardTo("CS2_231", EntityType.DECK, 2).Process(g, p, p.Hero, null);
            Check(g.HistoryFacts.Query.Shuffles().Count() == 7, "nested triggered shuffle separate operation");
            g.TriggerManager.ShuffleIntoDeckTrigger -= nest;
            while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_231")));
            new AddCardTo("CS2_231", EntityType.DECK, 3).Process(g, p, p.Hero, null);
            Check(g.HistoryFacts.Query.Shuffles().Count() == 8, "partial success counts once");
            new AddCardTo("CS2_231", EntityType.DECK, 2).Process(g, p, p.Hero, null);
            Check(g.HistoryFacts.Query.Shuffles().Count() == 8, "full deck no operation");
            bool rejected = false;
            try { p.Opponent.DeckZone.AddAtRandomPosition(Entity.FromCard(p.Opponent, Cards.FromId("CS2_231"))); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "unannotated insertion cannot silently become a shuffle");
            DiscoverCases(HistoryFactsProof.Fixture(history));
        }
        Console.WriteLine("PASS history operations: producer inventory, nested/partial/full shuffle, Discover/fixed/rejected/keep-all");
    }
    private static void DiscoverCases(Game g)
    {
        var p = g.Player1;
        var tradable=Add(p,"CORE_EX1_002");
        Check(g.Process(TradeCardTask.Any(p,tradable)) && !g.HistoryFacts.Query.Shuffles().Any(),"Trade is not gameplay shuffle");
        var context = new EffectContext(g, p.Id, p.Hero.Id);
        Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, ChoiceAction.HAND, new[] { Cards.FromId("CS2_231") }, null);
        Check(Generic.ChoicePick(p, g, p.Choice.Choices[0]), "fixed choice accepted");
        Check(!g.HistoryFacts.Query.Discoveries().Any(), "fixed choice not Discover");
        int continued = 0;
        context.Discover(new[] { Cards.FromId("CS2_231") }, afterChoose: new CustomTask((game, owner, source, target, stack) => {
            Check(game.HistoryFacts.Query.Discoveries().Single().SelectedEntityIds.Single() == target.Id,
                "Discover visible before real AfterChoose continuation"); continued++;
        }));
        var choice = p.Choice;
        var before = g.HistoryFacts.CanonicalText();
        Check(!Generic.ChoicePick(p, g, int.MaxValue) && before == g.HistoryFacts.CanonicalText(), "invalid legacy answer unchanged");
        Check(Generic.ChoicePick(p, g, choice.Choices[0]) && continued == 1, "singleton completes once");
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        context.Discover(new[] { Cards.FromId("CS2_231") });
        Generic.ChoicePick(p, g, p.Choice.Choices[0]);
        Check(g.HistoryFacts.Query.Discoveries().Count() == 2 && p.HandZone.Count == 10, "burned Discover selection still completes");
        Check(!context.Discover(Array.Empty<Card>()) && g.HistoryFacts.Query.Discoveries().Count() == 2, "empty Discover no completion");
        int entities=g.IdEntityDic.Count;
        new DiscoverTask(CardType.MINION,keepAllCondition:_=>true).Process(g,p,p.Hero,null);
        Check(g.HistoryFacts.Query.Discoveries().Count()==3 && g.HistoryFacts.Query.Discoveries().Last().KeepAll
            && g.IdEntityDic.Count==entities,"keep-all at capacity completes once without creating extra entities");
        var other=HistoryFactsProof.Fixture();var owner=other.Player1;int visibility=0;
        other.TriggerManager.ZoneTrigger += entity=>{
            if(entity is IPlayable entered && entered.Zone==owner.HandZone)
            {Check(other.HistoryFacts.Query.Discoveries().Single().KeepAll,"keep-all fact precedes hand ZONE callback");visibility++;}
        };
        new DiscoverTask(CardType.MINION,keepAllCondition:_=>true).Process(other,owner,owner.Hero,null);
        Check(visibility>0 && other.HistoryFacts.Query.Discoveries().Count()==1,"keep-all admitted members share one completion");

    }
}
