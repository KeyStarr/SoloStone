using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WhirlingStormdrakeProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_243", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source.HasWindfury && source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.DRAGON) && !source.IsImmune, "printed dual types and combat keywords; no prior turn match");
        ClearBoard(g, p); Play(g, p, "CS2_033"); End(g); End(g);
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsImmune, "secondary Elemental last turn activates Immune");
        source.TakeDamage(p.Opponent.Hero, 4); Core2026Proof.Check(source.Damage == 0, "Immune prevents damage");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(!((Minion)clone.IdEntityDic[source.Id]).IsImmune && source.IsImmune, "clone expiry isolated");
        Play(g, p, "BE_228", source); Core2026Proof.Check(!source.IsImmune && !source.HasWindfury, "Silence removes temporary Immune");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); End(g);
        Core2026Proof.Check(!source.IsImmune, "Immune expires on owner turn end"); End(g);
        ClearBoard(g, p); Play(g, p, "CS2_033"); End(g); End(g); source = (Minion)Play(g, p, id); Kill(g, source); End(g);
        Core2026Proof.Check(!source.IsImmune, "expiry safe after source leaves play");
    });
}
