using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 CORE_FP1_011. CORE-only token path; original FP1_011 untouched.
internal static class CoreWebspinnerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_FP1_011", (g, p, id) => {
        var source = Summon(p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 1 && p.HandZone.Count == 0, "summoning does not generate early");
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]);
            var output = next.Player1.HandZone.Single();
            CorePoolProof.Generated(next, next.Player1, id, output);
            Core2026Proof.Check(output.Card.IsRace(Race.BEAST) && next.Player2.HandZone.Count == 0, "friendly death adds one Beast only to its owner");
        });
        PoolPilotProof.EmptyHand(p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Deathrattle");
        source = Summon(p, id); p.SetasideZone.Add(p.BoardZone.Remove(source)); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == 0, "non-death departure generates nothing");
        var enemy = Summon(p.Opponent, id); Kill(g, enemy);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 1, "opposing death rewards only opposing owner");
        CorePoolProof.Generated(g, p.Opponent, id, p.Opponent.HandZone.Single());
        PoolPilotProof.FillHand(p); source = Summon(p, id); var kept = p.HandZone.Select(c => c.Id).ToArray(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Select(c => c.Id).SequenceEqual(kept), "full hand burns Beast without replacing cards");
    });
}
