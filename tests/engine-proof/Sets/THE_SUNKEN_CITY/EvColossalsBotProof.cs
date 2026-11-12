using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static partial class EvColossalsProof
{
    [GroupProof("ev-colossals-bot-fix")]
    internal static void Unpinned_FairBotChoices()
    {
        // Dropping an offered original's residence breaks Apply; retaining every deck identity leaks history.
        foreach (bool second in new[] { false, true }) foreach (int size in new[] { 1, 2, 3, 4 })
        {
            var g = Fresh(); if (second) End(g); var p = g.CurrentPlayer;
            EmptyHand(p); ClassDeck(p);
            for (int i = 0; i < size; i++)
            {
                var card = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
                card.AttackDamage = 10 + i; card.BaseHealth = 12 + i; card.Cost = i;
            }
            var originals = p.DeckZone.Select(c => c.Id).ToArray();
            var body = (Minion)Play(g, p, "TSC_030");
            var target = Summon(p.Opponent, "CS2_200");
            AttackWhenReady(g, body.Id, target.Id);
            int[] offers = p.Choice.Choices.ToArray();
            Core2026Proof.Check(offers.Length == Math.Min(3, size), "Dredge short/duplicate offers");
            Resume(g, next =>
            {
                var authoritative = next.SaveSnapshot();
                var fair = FairBotState.Create(next, p.PlayerId, 97, DeckStyle.Value);
                var projected = HistoryFairOriginProof.FairGame(fair);
                var opponent = HistoryFairOriginProof.FairGame(FairBotState.Create(next, p.Opponent.PlayerId, 97, DeckStyle.Value));
                foreach (int id in originals)
                {
                    bool known = originals.Take(offers.Length).Contains(id);
                    Core2026Proof.Check((projected.IdEntityDic[id] is Unknown) != known, "only bound offered deck originals stay visible");
                    Core2026Proof.Check(opponent.IdEntityDic[id] is Unknown, "opponent cannot read Dredge originals");
                    bool denied = false;
                    try { _ = opponent.HistoryFacts.Query.Residence(id, Zone.DECK); } catch (NotSupportedException) { denied = true; }
                    Core2026Proof.Check(denied, "opponent has no original residence link");
                }
                for (int i = 0; i < offers.Length; i++)
                {
                    var action = BotAction.From(ChooseTask.Pick(projected.CurrentPlayer, offers[i]));
                    var child = fair.Apply(action);
                    var resolved = HistoryFairOriginProof.FairGame(child);
                    var player = resolved.CurrentPlayer;
                    Core2026Proof.Check(player.Choice == null && player.DeckZone.Last().Id == originals[i], "fair Dredge moves each selected original to top");
                    var claw = ColossalRules.AppendagesOf((Minion)resolved.IdEntityDic[body.Id]).Single();
                    var attack = child.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == claw.Id && a.Target == target.Id);
                    var drawn = (Minion)HistoryFairOriginProof.FairGame(child.Apply(attack)).CurrentPlayer.HandZone.Single();
                    Core2026Proof.Check(drawn.Id == originals[i] && drawn.AttackDamage == 10 + i && drawn.Health == 12 + i && drawn.Cost == i,
                        "fair Claw draws selected original with modifications");
                }
                foreach (int id in originals)
                {
                    bool available = true;
                    try { _ = projected.HistoryFacts.Query.Residence(id, Zone.DECK); } catch (NotSupportedException) { available = false; }
                    Core2026Proof.Check(available == originals.Take(offers.Length).Contains(id), "history visibility agrees with offered original visibility");
                }
                var decision = BotSearch.Choose(fair, new BotBudget(2, 8, 32, int.MaxValue));
                Core2026Proof.Check(decision.Nodes >= offers.Length && fair.Actions.Contains(decision.Action), "BotSearch expands Dredge choices");
                _ = fair.Apply(decision.Action);
                Core2026Proof.Check(authoritative.SequenceEqual(next.SaveSnapshot()), "fair branches never mutate authoritative pending Dredge");
            });
        }
        // Each body enters through an actual fair play action on both seats; all crew rounds branch below.
        foreach (bool second in new[] { false, true }) foreach (string id in Ids)
        {
            var g = Fresh(); if (second) End(g); var p = g.CurrentPlayer;
            EmptyHand(p);
            p.BaseMana = 10; p.UsedMana = 0;
            var card = Add(p, id);
            var fair = FairBotState.Create(g, p.PlayerId, 97, DeckStyle.Value);
            var play = fair.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == card.Id && a.Position == 0);
            var child = fair.Apply(play);
            var game = HistoryFairOriginProof.FairGame(child);
            Core2026Proof.Check(game.IdEntityDic[card.Id].Zone == game.CurrentPlayer.BoardZone
                && game.CurrentPlayer.BoardZone.Count == Math.Min(7, 1 + card.Card.ColossalAppendageIds.Count), "fair body and appendages: " + id);
            var search = BotSearch.Choose(fair, new BotBudget(2, 8, 32, int.MaxValue));
            Core2026Proof.Check(search.Nodes > 0 && fair.Actions.Contains(search.Action), "BotSearch considers body play: " + id);
            _ = fair.Apply(search.Action);
            if (id == "TSC_660")
            {
                CrewBranches(child, card.Id, 0);
                // Also project a real pending decision at every round, including saved/restored cursors.
                PlayEntity(g, p, card);
                for (int round = 0; round < 3; round++)
                {
                    int completed = round;
                    Resume(g, next => {
                        CrewBranches(FairBotState.Create(next, p.PlayerId, 97, DeckStyle.Value), card.Id, completed);
                        Answer(next);
                    });
                    Core2026Proof.Check(PayloadRules.Entries(ColossalRules.AppendagesOf((Minion)card).Single(), NellieTheGreatThresher.Crew).Length == round + 1,
                        "authoritative crew selection completes one round");
                }
            }
            else Core2026Proof.Check(game.CurrentPlayer.Choice == null, "no other body creates pending choices: " + id);
            Console.WriteLine("PASS fair body Apply/BotSearch seat=" + p.PlayerId + " id=" + id);
        }
        // Existing proxy consumers must share the fix, including movement of rejected bound originals.
        foreach (bool second in new[] { false, true }) foreach (string id in new[] { "TIME_701", "TLC_451" })
        {
            var g = Fresh(); if (second) End(g); var p = g.CurrentPlayer; EmptyHand(p);
            DiscoverRealProof.Deck(p, "CS2_182", "CS2_200", "CS2_231", "CS2_029");
            Play(g, p, id);
            var fair = FairBotState.Create(g, p.PlayerId, 97, DeckStyle.Value);
            foreach (var action in fair.Actions)
            {
                var selected = ((ChooseTask)action.Resolve(p.Options())).Choices.Single();
                int bound = g.IdEntityDic[selected][DiscoverRealRules.BoundEntity];
                var child = HistoryFairOriginProof.FairGame(fair.Apply(action));
                Core2026Proof.Check(child.CurrentPlayer.Choice == null && child.CurrentPlayer.HandZone.Single().Id == bound
                    && child.CurrentPlayer.DeckZone.Count == 3, "existing bound deck consumer moves exact original: " + id);
            }
            var decision = BotSearch.Choose(fair, new BotBudget(1, 3, 3, int.MaxValue));
            Core2026Proof.Check(decision.Nodes == 3 && fair.Actions.Contains(decision.Action), "existing bound deck search: " + id);
        }
        Console.WriteLine("PASS Colossal owner choices: both seats, every Dredge pick, all three crew rounds, clone/snapshot, privacy");
        CrewKnowledgeAfterControl();
    }

    private static void CrewKnowledgeAfterControl()
    {
        var g=Fresh();var owner=g.Player1;
        var body=(Minion)Play(g,owner,"TSC_660");
        while(owner.Choice!=null) Answer(g);
        var ship=ColossalRules.AppendagesOf(body).Single();
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET,true)
            .Process(g,owner,owner.Hero,body);
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET,true)
            .Process(g,owner,owner.Hero,ship);
        g.ProcessTasks();g.DeathProcessingAndAuraUpdate();End(g);
        Resolve(g,body);
        while(g.CurrentPlayer.Choice!=null) Answer(g);
        var crew=PayloadRules.Entries(ship,NellieTheGreatThresher.Crew);
        Core2026Proof.Check(crew.Length==6,"repeated stolen Nellie retains old and newly selected crew");
        var view=HistoryFairOriginProof.FairGame(FairBotState.Create(g,2,97,DeckStyle.Value));
        var known=PayloadRules.Entries(view.IdEntityDic[ship.Id],NellieTheGreatThresher.Crew);
        Core2026Proof.Check(known.Length==3 && known.Select(c=>c.Card.Id).SequenceEqual(crew.Skip(3).Select(c=>c.Card.Id)),
            "new controller knows only its own three crew selections");
    }

    private static void CrewBranches(FairBotState fair, int bodyId, int completed)
    {
        var g = HistoryFairOriginProof.FairGame(fair); var p = g.CurrentPlayer;
        var ship = ColossalRules.AppendagesOf((Minion)g.IdEntityDic[bodyId]).Single();
        Core2026Proof.Check(PayloadRules.Entries(ship, NellieTheGreatThresher.Crew).Length == completed, "fair crew count advances");
        if (completed == 3)
        {
            Core2026Proof.Check(p.Choice == null, "fair crew decisions terminate after three");
            string[] names = PayloadRules.Entries(ship, NellieTheGreatThresher.Crew).Select(c => c.Card.Id).ToArray();
            Kill(g, ship);
            Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(names) && p.HandZone.All(c => c.Cost == 1), "fair Ship grants exact selected crew at one mana");
            return;
        }
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).Distinct().Count() == 3,
            "fair crew has three distinct offers");
        var decision = BotSearch.Choose(fair, new BotBudget(3 - completed, 3, 39, int.MaxValue));
        Core2026Proof.Check(decision.Nodes >= 3 && fair.Actions.Contains(decision.Action), "BotSearch expands pending crew decisions");
        foreach (var action in fair.Actions)
        {
            var child = fair.Apply(action);
            var next = HistoryFairOriginProof.FairGame(child);
            int selected = ((ChooseTask)action.Resolve(p.Options())).Choices.Single();
            string chosen = g.IdEntityDic[selected].Card.Id;
            var nextShip = next.IdEntityDic[ship.Id];
            Core2026Proof.Check(PayloadRules.Entries(nextShip, NellieTheGreatThresher.Crew).Last().Card.Id == chosen, "fair crew stores chosen definition");
            CrewBranches(child, bodyId, completed + 1);
        }
    }
}
