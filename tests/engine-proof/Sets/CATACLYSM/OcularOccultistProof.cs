using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OcularOccultistProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_490", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.Choice == null && p.DiscardedEntities.Count == 0, "printed Taunt; empty hand discards nothing");
        ClearBoard(g, p);
        var first = Add(p, "CS2_182"); var second = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var opponent = Add(p.Opponent, "CS2_182");
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { first.Id, second.Id, spell.Id }) &&
            p.Options().Count == 3 && p.Options().All(t => t is ChooseTask), "duplicates have distinct selectable identities, only friendly hand");
        var rng = g.Random.CaptureState();
        var clone = g.Clone(); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        PlayerDecisionProof.Answer(clone, second.Id);
        Core2026Proof.Check(second.Zone == p.HandZone && p.Choice != null && p.DiscardedEntities.Count == 0, "clone discard leaves original untouched");
        foreach (var candidate in new[] { g, restored }) PlayerDecisionProof.Answer(candidate, second.Id);
        Core2026Proof.Check(second.Zone == p.GraveyardZone && p.LastCardDiscarded == second.Id && p.DiscardedEntities.SequenceEqual(new[] { second.Id }),
            "chosen entity discarded through engine bookkeeping exactly once");
        Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { first.Id, spell.Id }) && opponent.Zone == p.Opponent.HandZone,
            "unselected and opponent cards preserved");
        Core2026Proof.Check(p.Choice == null && g.Random.CaptureState().Equals(rng) && Canonical.State(g) == Canonical.State(restored),
            "discard finishes without selection RNG; snapshot future matches");
        ClearBoard(g, p); Summon(p, "LOE_077"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, spell.Id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { first.Id }), "Brann excludes discarded card on second choice");
        PlayerDecisionProof.Answer(g, first.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && p.DiscardedEntities.Count == 3, "Brann discards two selected cards");
        ClearBoard(g, p); Summon(p, "LOE_077");
        var only = Add(p, "CS2_182"); Play(g, p, id); PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(p.Choice == null && p.DiscardedEntities.Count == 4, "Brann singleton skips now-empty second choice");
        ClearBoard(g, p);
        // Malchezaar's Imp checks that the selected discard raises normal discard reactions.
        Summon(p, "KAR_089"); var victim = Add(p, "CS2_029"); int deck = p.DeckZone.Count;
        Play(g, p, id); PlayerDecisionProof.Answer(g, victim.Id);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1 && victim.Zone == p.GraveyardZone,
            "selected discard triggers Malchezaar's Imp draw");
    });
}
