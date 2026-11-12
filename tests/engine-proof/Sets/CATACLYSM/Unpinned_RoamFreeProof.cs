using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_RoamFreeProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_307", (g, p, id) => {
        Play(g, p, "MEND_300");
        Play(g, p, "MEND_303");
        Play(g, p, id);
        var roster = CompanionRules.Roster(p);
        Core2026Proof.Check(roster.Length == 3 && roster.All(x => Cards.FromId(x).Cost == 7)
            && p.Choice.Choices.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(roster),
            "pinned Pet/Elekk/Roam reaches seven and offers locked roster slots");
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        int choice = p.Choice.Choices[1]; string selected = g.IdEntityDic[choice].Card.Id;
        var rng = g.Random.CaptureState();
        foreach (var copy in new[] { g, clone, loaded }) PlayerDecisionProof.Answer(copy, choice);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Last().Card.Id == selected
            && g.Random.CaptureState().Equals(rng) && Canonical.State(g) == Canonical.State(clone)
            && Canonical.State(g) == Canonical.State(loaded), "choice survives clone/snapshot and summons selected without reroll");
        ParkBoard(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id); var capped = CompanionRules.Roster(p);
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.BoardZone.Count == 7 && capped.All(x => Cards.FromId(x).Cost == 9),
            "full board still upgrades; failed summon does not undo roster");
    });
}
