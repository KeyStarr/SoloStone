using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamPoolProof;

internal static class SelenicDrakeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_462", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0 && source.CantBeTargetedBySpells && source.CantBeTargetedByHeroPowers, "Elusive; no generation on play");
        var trigger = source.Card.Power.Trigger.SingleTask;
        RandomHandBoundaries(g, p, id, () => {
            trigger.Process(g, p, source, null); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        });
        PoolPilotProof.Resume(g, next => End(next));
        Generated(g, source, p.HandZone, 1);
        int count = p.HandZone.Count;
        End(g);
        Core2026Proof.Check(p.HandZone.Count == count + 1, "opponent end only adds normal next-turn draw");
        PoolPilotProof.EmptyHand(p);
        End(g); Generated(g, source, p.HandZone, 1);
        End(g); PoolPilotProof.EmptyHand(p); source.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g);
        Core2026Proof.Check(p.HandZone.Count == 0 && !source.CantBeTargetedBySpells && source.ActivatedTrigger == null, "Silence removes Elusive and trigger");
        End(g); PoolPilotProof.EmptyHand(p); Kill(g, source);
        source = (Minion)Play(g, p, id); Kill(g, source); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "removed source stops generation");
        End(g); PoolPilotProof.EmptyHand(p); Add(p, id); End(g);
        Core2026Proof.Check(p.HandZone.Count == 1, "hand source does not trigger");
    });
}
