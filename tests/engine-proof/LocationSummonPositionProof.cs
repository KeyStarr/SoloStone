using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class LocationSummonPositionProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        // Every implemented location with a direct minion summon, including token/staged locations.
        foreach (string id in new[] { "JAIL_877", "JAIL_511", "BE_561", "BE_780", "FIR_907", "EDR_454",
            "TIME_436", "TIME_436t1", "TIME_436t2", "TIME_211t2", "TIME_211t2t", "TIME_890t2", "CATA_492", "JAIL_887",
            "CATA_527", "TLC_433t2", "TLC_100t1", "TLC_100t2", "TLC_100t3" })
        foreach (bool final in new[] { false, true })
        foreach (bool triggered in id == "JAIL_887" ? new[] { false, true } : new[] { false })
        foreach (bool sparse in id == "TLC_100t3" ? new[] { false, true } : new[] { false })
        {
            try { SummonFromLocation(id, final, triggered, sparse); }
            catch (Exception e) { failures.Add($"{id} final={final} triggered={triggered} sparse={sparse}: {e.Message}"); }
        }
        L.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS location summon positions and creation order: 19 definitions, 42 cases, living/final charge or Deathrattle, mixed/sparse rows");
    }

    private static void SummonFromLocation(string id, bool final, bool triggered, bool sparse)
    {
        var game = L.Game(); var p = game.Player1;
        var left = L.Minion(p, "EX1_043");
        Card definition = Cards.FromId(id);
        bool composite = id.StartsWith("TLC_100");
        if (composite)
            definition = CompositeLocationRules.Compose(definition, Cards.FromId(id + "5"),
                Cards.FromId(id + (id == "TLC_100t1" ? "6" : "7")));
        var source = (BoardObject)Entity.FromCard(p, definition, zone: p.BoardZone);
        var right = sparse ? null : L.Minion(p);
        int slot = source.ZonePosition;
        var existing = p.BoardZone.Select(m => m.Id).ToHashSet();
        bool deathrattle = triggered || id is "CATA_527" or "TLC_433t2";
        if (deathrattle)
        {
            if (final) { source.Destroy(); game.DeathProcessingAndAuraUpdate(); }
            else source.Power.DeathrattleTask.Process(game, p, source, null, new TaskStack());
        }
        else
        {
            source.Location.ConditionLocked = false;
            B16AutoplayProof.Ready(source);
            if (final) source[GameTag.DAMAGE] = source[GameTag.HEALTH] - 1;
            L.Use(source, id is "EDR_454" or "TIME_211t2" or "TIME_211t2t" || composite && id != "TLC_100t1" ? left : null);
            while (p.Choice != null) B16AutoplayProof.Answer(game, id == "JAIL_887" ? p.HandZone.First().Id : 0);
        }
        var summons = p.BoardZone.Where(m => !existing.Contains(m.Id)).ToArray();
        int expected = id == "TIME_890t2" ? 2 : composite ? (id == "TLC_100t1" ? 1 : id == "TLC_100t2" ? 3 : final || sparse ? 5 : 4) : !triggered && final && id == "JAIL_887" ? 2 : 1;
        // With both neighbours, a living Fire Plume Ridge fills the row before its copy component can summon.
        L.Check(summons.Length == expected, $"expected {expected} summons, got {summons.Length}");
        L.Check(summons.Select(m => m.Id).SequenceEqual(summons.Select(m => m.Id).OrderBy(id => id)),
            "summons stay left to right in creation order: " + string.Join(", ", summons.Select(m => $"{m.Card.Id}#{m.Id}")));
        bool remains = !final;
        L.Check(left.ZonePosition == 0 && summons[0].ZonePosition == slot + (remains ? 1 : 0),
            "first summon is right of living location or in retired location's slot");
        L.Check((source.Zone == p.BoardZone) == remains, "location survives only the non-final case");
        L.Check(!remains || source.ZonePosition == slot, "living location keeps its slot, before its summons");
        L.Check(right == null || right.ZonePosition == slot + summons.Length + (remains ? 1 : 0), "right neighbour shifts past summons/location");
        L.Check(p.BoardZone.Slots.Select(e => e.ZonePosition).SequenceEqual(Enumerable.Range(0, p.BoardZone.SlotCount)), "contiguous physical slots");
    }
}
