using SabberStoneCore.Enums;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class ContingencyProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_023", (g, p, id) => {
        EmptyDeck(p); var bottom = InDeck(p, "CS2_171"); var second = InDeck(p, "CS2_182"); var top = InDeck(p, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { bottom.Id, second.Id }) && p.DeckZone.Single() == top, "bottom two in order; top remains");
        Play(g, p, id); Core2026Proof.Check(top.Zone == p.HandZone && p.Hero.Fatigue == 1 && p.Hero.Damage == 1, "one card then one fatigue attempt");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Fatigue == 3 && p.Hero.Damage == 6, "empty deck attempts two draws");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); bottom = InDeck(p, "CS2_171"); second = InDeck(p, "CS2_200");
        var removed = p.HandZone[0]; p.HandZone.Remove(removed); p.SetasideZone.Add(removed);
        Play(g, p, id); Core2026Proof.Check(bottom.Zone == p.HandZone && second.Zone == p.GraveyardZone && p.HandZone.Count == 10, "bottom draw obeys hand limit");
    });
}
