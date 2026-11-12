using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadewingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_993", (g, p, id) => {
        var shadow = Add(p, "CORE_ICC_055"); var fire = Add(p, "CORE_CS2_029"); var minion = Add(p, "CS2_182");
        var source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(shadow.Cost == 0 && fire.Cost == 4 && minion.Cost == 4, "only Shadow spell discounted by two with zero floor");
        var second = Add(p, "CORE_ICC_055"); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(shadow.Cost == 0 && (second.Cost == 0 || second.Cost == 2), "random eligible Shadow spell only");
        string original = HsSim.Canonical.State(g);
        var clone = g.Clone(); var cp = clone.Player1; var third = Add(cp, "CORE_ICC_055"); Kill(clone, Summon(cp, id));
        Core2026Proof.Check(HsSim.Canonical.State(g) == original, "clone discount does not mutate original");
        source = Summon(p, id); Play(g, p, "BE_228", source); int cost = second.Cost; Kill(g, source);
        Core2026Proof.Check(second.Cost == cost, "silence suppresses Deathrattle");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Add(p, "CS2_182"); Add(p, "CORE_CS2_029"); Kill(g, Summon(p, id));
        Core2026Proof.Check(p.HandZone[0].Cost == 4 && p.HandZone[1].Cost == 4, "no eligible hand spell no-op");
        var costly = Add(p, "BE_873"); Kill(g, Summon(p, id));
        Core2026Proof.Check(costly.Cost == 2, "four-cost Shadow spell reduced by exactly two");
        End(g); End(g); Core2026Proof.Check(costly.Cost == 2, "discount persists across turns");
    });
}
