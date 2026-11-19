using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

// #193: every non-final use skips the owner's next turn (repo rules record "loc-cooldown"; tag 2353 set on use,
// cleared at the owner's next turn start, EXHAUSTED cleared the turn after). Locations whose script never set the tag reopened one turn early.
internal static class LocationCooldownProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var id in new[] { "CATA_492", "TIME_890t2", "EDR_454", "EDR_520", "MEND_044", "JAIL_987", "COMPOSITE" })
            try { OwnerTurnCycle(id); } catch (Exception e) { failures.Add(id + ": " + e.Message); }
        L.Check(failures.Count == 0, "cooldown " + string.Join(" | ", failures));
        Registry();
        WelcomeHomeSlotAfterSameBatchDeaths();
        Console.WriteLine("PASS location cooldown (#193)");
    }

    private static BoardObject Setup(Game g, string id)
    {
        var p = g.Player1;
        L.Minion(p); L.Minion(p); L.Minion(p.Opponent);
        if (id == "EDR_454") L.Minion(p, "EX1_043"); // wants a friendly Dragon
        if (id == "COMPOSITE") return B18Autoplay2Proof.Composite(p, 2, 4, 5);
        var location = L.Location(p, id);
        L.Check(location.Durability >= 2, id + " needs a non-final charge for this test (durability " + location.Durability + ")");
        return location;
    }

    private static bool Use(Game g, BoardObject location)
    {
        var p = location.Controller;
        var option = p.Options().OfType<ActivateLocationTask>().FirstOrDefault(t => t.Source == location);
        if (option == null) return false;
        L.Check(g.Process(option), "activate");
        for (int guard = 0; p.Choice != null && guard < 10; guard++) B16AutoplayProof.Answer(g);
        return true;
    }

    private static void OwnerTurnCycle(string id)
    {
        var g = L.Game(); var p = g.Player1;
        var location = Setup(g, id);
        L.Check(Use(g, location), "no activation option");
        if (location.Zone != p.BoardZone) return; // spent or removed by its own effect: nothing to time
        L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 1,
            $"closed after use (cooldown {location[GameTag.LOCATION_ACTION_COOLDOWN]})");
        L.End(g);
        L.Check(location[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "opponent turn keeps cooldown");
        L.End(g);
        L.Check(location.IsExhausted && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0 && !LocationRules.CanActivate(p, location, null)
            && !p.Options().OfType<ActivateLocationTask>().Any(t => t.Source == location), "owner next turn: still closed (ajar)");
        L.End(g); L.End(g);
        L.Check(!location.IsExhausted && location[GameTag.EXHAUSTED] == 0 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0
            && p.Options().OfType<ActivateLocationTask>().Any(t => t.Source == location), "owner turn after next: open again");
    }

    // Exposed by the cooldown change in the transport corpus (standard-batch-1-39-vs-45-s8): Welcome Home's Deathrattle summoned at the
    // location's last slot even when minions that died in the same batch had already shrunk the board (ZoneException: Invalid board insertion).
    private static void WelcomeHomeSlotAfterSameBatchDeaths()
    {
        var g = L.Game(); var p = g.Player1;
        var a = L.Minion(p); var b = L.Minion(p);
        var location = L.Location(p);
        var spell = Entity.FromCard(p, Cards.FromId("TIME_EVENT_997"), zone: p.SetasideZone);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_EVENT_997e"), spell, location);
        L.Check(location.ZonePosition == 2, "location is the third slot");
        a.Destroy(); b.Destroy(); location.Destroy();
        g.DeathProcessingAndAuraUpdate();
        L.Check(p.BoardZone.Count == 1 && p.BoardZone.GetSlot(0).Card.Cost == 3, "Welcome Home summons into the shrunk board (count " + p.BoardZone.Count + ")");
    }

    // Catches future locations: any implemented activation location with a non-final charge must be on cooldown after use.
    private static void Registry()
    {
        var bad = new List<string>(); var thrown = new List<string>(); int tried = 0;
        foreach (var card in Cards.All.Where(c => c.Type == CardType.LOCATION && c.Implemented && c.Power?.LocationActivation?.Task != null && c.Id != "TLC_100t1" && c.Id != "TLC_100t2" && c.Id != "TLC_100t3"))
        {
            try
            {
                var g = L.Game(); var p = g.Player1;
                L.Minion(p); L.Minion(p); L.Minion(p.Opponent);
                var location = L.Location(p, card.Id);
                if (location.Durability < 2) continue;
                if (!Use(g, location) || location.Zone != p.BoardZone || location.Durability < 1) continue;
                tried++;
                if (location[GameTag.LOCATION_ACTION_COOLDOWN] != 1)
                    bad.Add(card.Id);
            }
            catch (Exception e) { thrown.Add(card.Id + ": " + e.GetType().Name + " " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
        }
        L.Check(thrown.Count == 0, "locations that throw on use: " + string.Join(" | ", thrown));
        L.Check(tried > 20, "registry sweep reached only " + tried + " locations");
        L.Check(bad.Count == 0, "locations still usable right after use / no cooldown: " + string.Join(",", bad));
    }
}
