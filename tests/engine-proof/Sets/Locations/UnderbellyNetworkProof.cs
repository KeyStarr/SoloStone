using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class UnderbellyNetworkProof
{
    internal static void StandardCase() => StandardSetsProof.Case("JAIL_877", (game, p, id) => {
        var network = L.Play(p, id);
        L.Use(network);
        var rat = p.BoardZone.Single();
        L.Check(rat.Card.Id == "JAIL_877t" && rat.AttackDamage == 2 && rat.Health == 1 && rat.HasDeathrattle, "Network summons 2/1 Rat with Deathrattle");
        int hand = p.HandZone.Count, deck = p.DeckZone.Count;
        rat.Destroy(); game.DeathProcessingAndAuraUpdate();
        L.Check(rat.Zone == p.GraveyardZone && p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1, "Rat Deathrattle draws exactly one");
        L.End(game);
        L.Check(game.Step == Step.MAIN_ACTION && game.DeadBoardObjects.Count == 0, "Network reaches snapshot boundary");
    });

    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = L.Game(seed); var p = game.Player1; var network = L.Play(p, "JAIL_877");
            L.Use(network);
            var rat = p.BoardZone.Single();
            L.Check(rat.Card.Id == "JAIL_877t" && rat.AttackDamage == 2 && rat.Health == 1 && rat.HasDeathrattle, "2/1 Rat summoned");
            L.Check(network.Durability == 1 && !LocationRules.CanActivate(p, network, null), "closed network cannot repeat");
            int hand = p.HandZone.Count, deck = p.DeckZone.Count;
            rat.Destroy(); game.DeathProcessingAndAuraUpdate();
            L.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1, "Rat Deathrattle draws exactly one");
            L.End(game); L.End(game); L.End(game); L.End(game); L.Use(network);
            L.Check(network.Zone == p.GraveyardZone && p.BoardZone.Single().Card.Id == "JAIL_877t", "final charge summons and retires");
        }
        Console.WriteLine("PASS Underbelly Network seeds 1-3 summon/closed/final charge/Rat draw");
    }
}
