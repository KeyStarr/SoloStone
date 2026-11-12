using SabberStoneCore.Enums;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class RafaamLadderProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_031", (g, p, id) => {
        EmptyDeck(p); InDeck(p, "CS2_171"); InDeck(p, "CS2_171"); InDeck(p, "CS2_182"); InDeck(p, "CS2_200");
        var clone = g.CloneExact(history: true); Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.Select(c => c.Cost).Distinct().Count() == 3 && p.DeckZone.Count == 1, "draws three different costs");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "random draws deterministic through clone");
        EmptyDeck(p); InDeck(p, "CS2_171"); InDeck(p, "CS2_171"); int hand = p.HandZone.Count;
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == 1 && p.Hero.Fatigue == 0, "one cost draws once with no extra fatigue");
        EmptyDeck(p); hand = p.HandZone.Count; Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == hand && p.Hero.Fatigue == 0, "no matching cards draws none");
    });
}
