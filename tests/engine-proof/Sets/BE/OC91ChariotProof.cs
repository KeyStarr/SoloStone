using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OC91ChariotProof
{
    internal static void Run() => StandardSetsProof.Case("BE_701", (g, p, id) => {
        int deck = p.DeckZone.Count;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck && source.IsRace(Race.MECHANICAL), "printed Mech does not draw for its own play");
        Play(g, p, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "friendly minion play draws exactly one");
        Play(g, p, "CS2_008", p.Opponent.Hero); Summon(p, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "spell and non-play summon never draw");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_171");
        Core2026Proof.Check(clone.Player1.DeckZone.Count == deck - 2 && p.DeckZone.Count == deck - 1, "clone play trigger isolated");
        End(g); deck = p.DeckZone.Count; Play(g, p.Opponent, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck, "opponent minion play never draws");
        End(g); deck = p.DeckZone.Count; Play(g, p, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "trigger persists across turns");
        source.Silence(); deck = p.DeckZone.Count; Play(g, p, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck, "Silence removes draw trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source); Add(p, id);
        deck = p.DeckZone.Count; Play(g, p, "CS2_171");
        Core2026Proof.Check(p.DeckZone.Count == deck, "dead and held copies never trigger");
    });
}
