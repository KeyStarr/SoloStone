using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class DaydreamingPixieProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_530", (g, p, id) => {
        SourceExcluded(g, id);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "no generation on play");
        Resume(g, next => End(next));
        Generated(g, source, p.HandZone, 1);
        int count = p.HandZone.Count; End(g);
        Core2026Proof.Check(p.HandZone.Count == count + 1, "opponent end only draws owner's next-turn card");
        EmptyHand(p);
        Pool(g, id, 0, () => {
            var before = g.Random.DrawCount; End(g);
            Core2026Proof.Check(p.HandZone.Count == 0 && g.Random.DrawCount == before, "empty turn-end pool creates no card/RNG");
        });
        End(g); EmptyHand(p); FillHand(p);
        long random = g.Random.DrawCount; End(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - random == 1, "full hand still samples at owner end");
        End(g); EmptyHand(p); source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence stops trigger");
        End(g); EmptyHand(p); var removed = (Minion)Play(g, p, id); Kill(g, removed); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "dead source stops trigger");
    });
}
