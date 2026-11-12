using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
namespace TransportInvariants;

internal static class DiscoverRealProof
{
    internal static void Run()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "JAIL_734", "CAP_403", "JAIL_303", "JAIL_851", "TIME_041", "TIME_701", "CATA_591", "BE_555" })
        {
            var game = OfflinePoolTestRecipes.NewGame(opponent, hero: CardClass.HUNTER); var owner = game.CurrentPlayer;
            foreach (var p in new[] { owner, owner.Opponent })
            {
                foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
                foreach (string card in new[] { "CS2_182", "CS2_029", "CS2_200" })
                {
                    Generic.DrawCard(p, Cards.FromId(card));
                    SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
                }
            }
            NativeSession session = null;
            var match = new NativeMatch((number, packet) => session.Receive(number, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
            var source = Generic.DrawCard(owner, Cards.FromId(id)); owner.BaseMana = 10;
            session.Flush(); game.PowerHistory.Last.Clear();
            session.Process(PlayCardTask.Any(owner, source));
            if (id == "CATA_591") { session.Process(EndTurnTask.Any(owner)); session.Process(EndTurnTask.Any(owner.Opponent)); }
            var offers = owner.Choice.Choices.ToArray();
            SelfChecks.Check(offers.All(i => opponent ? session.Client.Entities[i].CardId == "" : session.Client.Entities[i].CardId == game.IdEntityDic[i].Card.Id),
                "real entity offers private to chooser " + id);
            session.PublishDecision(owner);
            int picks = 0;
            while (owner.Choice != null)
            {
                if (id == "BE_555") game.IdEntityDic[owner.Choice.Choices[0]].Cost = 8;
                session.Process(ChooseTask.Pick(owner, owner.Choice.Choices[0]));
                SelfChecks.Check(++picks <= 10, "real choice makes progress " + id);
            }
            var violations = Invariants.Compare(game, session.Client).ToArray();
            SelfChecks.Check(violations.Length == 0, "real Discover transport " + id + ": " + string.Join("; ", violations.Select(v => v.ToString())));
            if (id != "BE_555")
                SelfChecks.Check(offers.All(i => opponent ? session.Client.Entities[i].CardId == "" : session.Client.Entities[i].CardId.Length != 0),
                    "retired offers retain privacy " + id);
            session.History.Finish();
        }
        Console.WriteLine("PASS real entity Discover transport: 8 cards, both choosers, private state and movement");
    }
}
