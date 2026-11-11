using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamPoolProof;

internal static class InfernoHeraldProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_913", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Play(g, p, "GAME_005"); Play(g, p, "CS2_025");
        Core2026Proof.Check(p.HandZone.Count == 0, "schoolless and Frost spells excluded; no generation on play");
        End(g); Play(g, p.Opponent, "CS2_029", p.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "opponent Fire spell excluded");
        End(g); PoolPilotProof.EmptyHand(p);
        var trigger = source.Card.Power.Trigger.SingleTask;
        RandomHandBoundaries(g, p, id, () => { trigger.Process(g, p, source, null); g.ProcessTasks(); });
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, "CS2_029", next.Player2.Hero));
        Generated(g, source, p.HandZone, 1, discount: 3);
        var generated = p.HandZone[0];
        End(g); End(g);
        Generated(g, source, new[] { generated }, 1, discount: 3);
        PoolPilotProof.EmptyHand(p);
        Play(g, p, "FIR_910", p.Opponent.Hero);
        Generated(g, source, p.HandZone, 1, discount: 3);
        PoolPilotProof.EmptyHand(p); source.Silence(); g.DeathProcessingAndAuraUpdate();
        Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0 && source.ActivatedTrigger == null, "Silence stops generation");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "removed source stops generation");
        source = (Minion)Add(p, id); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 1, "source in hand cannot trigger");
        PlayEntity(g, p, source); Play(g, p, "CS2_029", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.HandZone.Count == 0, "Fire spell killing source prevents after-cast trigger");
    });
}
