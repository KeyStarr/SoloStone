using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class SemiStablePortalProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_000", (g, p, id) => {
        TimeTravelPoolProof.RandomBoundaries(g, p, id, source => {
            source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
            g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        }, discount: 3);
        foreach (var fixture in new[] { ("CORE_GIL_531", 0), ("CORE_EX1_014", 0), ("TIME_872", 5), ("CORE_TSC_076", 4) })
        {
            // The last fixture is a spell: unrestricted minion generation must exclude it.
            WithCards(g, new[] { fixture.Item1 }, () => {
                EmptyHand(p); Resolve(g, p, id);
                Core2026Proof.Check(p.HandZone.Count == (fixture.Item1 == "CORE_TSC_076" ? 0 : 1), "minion-only unrestricted pool");
                if (p.HandZone.Count > 0) Core2026Proof.Check(p.HandZone.Single().Cost == fixture.Item2, "discount floors at zero");
            });
        }
        EmptyHand(p);
        var source = Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        TimeTravelPoolProof.RandomGenerated(g, source, p.HandZone, 1, discount: 3);
    });
}
