using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class EnduringRoachProof
{
    internal static void Run() => StandardSetsProof.Case("END_008", (g, p, id) => {
        var held = (Minion)Add(p, id); var source = (Minion)Play(g, p, id);
        p.UsedMana = 6;
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(clone.Process(clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER)), "clone power accepted");
        Core2026Proof.Check(clone.Player1.UsedMana == 6 && p.UsedMana == 6, "clone pays two then refreshes two independently");
        Core2026Proof.Check(g.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER)), "power accepted");
        Core2026Proof.Check(p.UsedMana == 6 && held.Zone == p.HandZone, "board Roach refreshes two after power");
        End(g); UsePower(g, p.Opponent); Core2026Proof.Check(p.Opponent.UsedMana == 2, "opponent power does not refresh");
        End(g); source.Silence(); UsePower(g, p); Core2026Proof.Check(p.UsedMana == 2, "Silence removes refresh");
        Kill(g, source); var dead = Summon(p, id); Kill(g, dead); End(g); End(g); UsePower(g, p);
        Core2026Proof.Check(p.UsedMana == 2, "dead source and hand copy do not trigger");
        Summon(p, id); Summon(p, id); End(g); End(g); UsePower(g, p);
        Core2026Proof.Check(p.UsedMana == 0 && p.RemainingMana == 10, "multiple refreshes clamp at zero used mana");
    });
}
