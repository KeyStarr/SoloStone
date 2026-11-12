using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static TimeTravelPoolProof;
using static PoolPilotProof;

internal static class VoodooTotemProof
{
    internal static void Run() => StandardSetsProof.Case("END_029", (g, p, id) => {
        RandomBoundaries(g, p, id, source => {
            source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack());
            g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        });
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "no spell before end of turn");
        Resume(g, End); RandomGenerated(g, source, p.HandZone, 1);
        var generated = p.HandZone.Single(); End(g);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Contains(generated), "opponent turn adds only normal draw");
        EmptyHand(p); source.Silence(); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes end-turn generation"); End(g);
        EmptyHand(p); source = Summon(p, id); Kill(g, source); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "dead source cannot generate"); End(g);
        source = Summon(p, id); EmptyHand(p); FillHand(p); long draws = g.Random.DrawCount; End(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == draws + 1, "active trigger respects full hand");
    });
}
