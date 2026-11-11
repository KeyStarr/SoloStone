using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class IridaSinseekerProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("JAIL_719", (g, p, id) => {
        foreach (int count in new[] { 0, 1 })
        {
            var edge = HeroDivineShieldProof.NewGame(true); var owner = edge.Player1;
            while (owner.DeckZone.Count > count) owner.SetasideZone.Add(owner.DeckZone.Remove(owner.DeckZone.TopCard));
            var rng = edge.Random.CaptureState(); Play(edge, owner, id);
            Core2026Proof.Check(owner.DeckZone.Count == count && rng.Equals(edge.Random.CaptureState())
                && !(owner.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_719e2") ?? false), "Unpinned_EmptyAndSingleton no exile or RNG");
        }
        var ordered = p.DeckZone.Reverse().ToArray();
        Core2026Proof.Check(ordered[0].Id == p.DeckZone.TopCard.Id, "ledger starts with actual deck top");
        var expectedRandom = g.Random.Clone();
        int expectedSurvivor = ordered[expectedRandom.Next(ordered.Length)].Id;
        var source = Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == 1, "Unpinned_UniformSurvivor exactly one remains");
        var survivor = p.DeckZone.Single();
        Core2026Proof.Check(survivor.Id == expectedSurvivor,
            "Unpinned_UniformSurvivor seeded entity identity, not fixed top/bottom");
        Core2026Proof.Check(g.Random.CaptureState().Equals(expectedRandom.CaptureState()),
            "Unpinned_UniformSurvivor advances RNG by exactly one bounded choice");
        Kill(g, (Minion)source);
        var voidActor = p.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_719e2");
        Core2026Proof.Check(PayloadRules.Entries(voidActor).Select(e => e[PayloadRules.Link]).SequenceEqual(ordered.Where(e => e.Id != survivor.Id).Select(e => e.Id)), "original deck order");
        End(g); End(g);
        Core2026Proof.Check(p.HandZone.Count == 3 && PayloadRules.Entries(voidActor).Length == ordered.Length - 3, "Unpinned_OrderedFinite two before normal draw after source death");
        while (p.HandZone.Count < 10) Add(p, "CS2_120");
        int remaining = PayloadRules.Entries(voidActor).Length; End(g); End(g);
        Core2026Proof.Check(PayloadRules.Entries(voidActor).Length == remaining - 2 && p.HandZone.Count == 10, "Unpinned_FullHand consumes entries without queue");
    });
}
