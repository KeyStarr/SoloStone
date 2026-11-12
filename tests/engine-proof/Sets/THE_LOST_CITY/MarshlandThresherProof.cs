using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MarshlandThresherProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_256", (g, p, id) => {
        var held = (Minion)Add(p, id); var source = (Minion)Play(g, p, id); var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, "GAME_005");
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).HasDivineShield && !source.HasDivineShield, "clone trigger isolated");
        Play(g, p, "GAME_005"); Core2026Proof.Check(source.HasDivineShield && !held.HasDivineShield, "friendly spell grants Shield only on board");
        source.HasDivineShield = false; Play(g, p, "CS2_182"); Core2026Proof.Check(!source.HasDivineShield, "minion play does not trigger");
        End(g); Play(g, p.Opponent, "GAME_005"); Core2026Proof.Check(!source.HasDivineShield, "opponent spell does not trigger");
        End(g); Play(g, p, "GAME_005"); End(g); Core2026Proof.Check(source.HasDivineShield, "Shield persists at end of turn"); End(g);
        Play(g, p, "BE_228", source); Play(g, p, "GAME_005"); Core2026Proof.Check(!source.HasDivineShield, "Silence removes trigger and Shield");
        var dead = (Minion)Play(g, p, id); Kill(g, dead); Play(g, p, "GAME_005"); Core2026Proof.Check(!dead.HasDivineShield, "dead source cannot trigger");
        var dying = (Minion)Play(g, p, id); Play(g, p, "CS2_029", dying); Core2026Proof.Check(dying.Zone == p.GraveyardZone && !dying.HasDivineShield, "lethal spell cannot grant posthumous Shield");
    });
}
