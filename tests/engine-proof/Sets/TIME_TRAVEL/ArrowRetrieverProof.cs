using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArrowRetrieverProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_601", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 3, "draws from zero to three cards");
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 3, "already three draws none");
        Add(p, "CS2_182"); Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 4, "above three draws none");
        foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
        while (p.DeckZone.Count > 1) { var c = p.DeckZone[0]; p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 1 && p.DeckZone.Count == 0 && p.Hero.Damage == 3, "short deck attempts both missing draws through fatigue");
    });
}
