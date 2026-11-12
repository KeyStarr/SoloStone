using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class DeferredHistoryCardsProof
{
    internal static void Run()
    {
        const string id = "TIME_620";
        var card = Cards.All.SingleOrDefault(c => c.Id == id);
        // Keep the scheduling regression executable if this deferred card is registered again.
        if (card?.Implemented == true)
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var own = (Minion)Play(g, p, "CS2_182"); Play(g, p, id);
            p.NumTurnsLeft = 2; End(g); Kill(g, own);
            var death = g.HistoryFacts.Query.Deaths().Last();
            Core2026Proof.Check(death.FromPlayId != 0 && death.PlayedTurn + 1 == death.Turn
                && p.SecretZone.Count == 1, "Untimely Death rejects otherwise-eligible own-turn death");
            End(g);
            var sameTurn = (Minion)Play(g, p.Opponent, "CS2_182");
            new ControlTask(EntityType.TARGET).Process(g, p, p.Hero, sameTurn);
            Kill(g, sameTurn); death = g.HistoryFacts.Query.Deaths().Last();
            Core2026Proof.Check(death.FromPlayId != 0 && death.PlayedTurn == death.Turn
                && death.ControllerId == p.Id && p.SecretZone.Count == 1,
                "Untimely Death rejects same-turn hand-play during opponent turn");
            var enemy = (Minion)Play(g, p.Opponent, "CS2_182");
            p.Opponent.NumTurnsLeft = 2; End(g); Kill(g, enemy);
            death = g.HistoryFacts.Query.Deaths().Last();
            Core2026Proof.Check(death.FromPlayId != 0 && death.PlayedTurn + 1 == death.Turn
                && death.ControllerId == p.Opponent.Id && p.SecretZone.Count == 1,
                "Untimely Death rejects otherwise-eligible enemy-side death");

            g = HistoryFactsProof.Fixture(); p = g.Player1;
            var abomination = (Minion)Play(g, p, "EX1_097"); Play(g, p, id);
            End(g); Kill(g, abomination);
            var body = p.BoardZone.Single();
            Core2026Proof.Check(p.SecretZone.Count == 0 && body.Card.Id == "EX1_097" && body.AttackDamage == 4 && body.Health == 4,
                "Untimely Death must resolve AFTER old Abomination Deathrattle; resummoned body must be 4/4, got "
                + body.AttackDamage + "/" + body.Health);
            Console.WriteLine("PASS Untimely Death continuation and owner/global-turn exclusions");
            return;
        }
        Core2026Proof.Check(card?.Implemented != true && !StandardSets.CollectibleIds.Contains(id)
            && !EffectModules.ByDbfId.ContainsKey(121210),
            "TIME_620 stays excluded pending core death/secret scheduling fix");
        Console.WriteLine("PASS deferred history cards: TIME_620 excluded pending death/secret scheduling fix");
    }
}
