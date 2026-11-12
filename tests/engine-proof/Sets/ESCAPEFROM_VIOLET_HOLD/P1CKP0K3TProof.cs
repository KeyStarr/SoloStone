using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class P1CKP0K3TProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_456", (g, p, id) => {
        while (p.DeckZone.Count > 25) { var c = p.DeckZone[0]; p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == 24 && p.HandZone.Count == 1, "25 cards draws exactly one");
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == 24 && p.HandZone.Count == 1, "24 cards draws none");
    });
}
