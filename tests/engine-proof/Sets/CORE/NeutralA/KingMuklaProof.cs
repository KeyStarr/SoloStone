using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class KingMuklaProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_014", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.HandZone.Count == 2 && p.Opponent.HandZone.All(c => c.Card.Id == "EX1_014t" && c.Card.AssetId == 1694 && c.Cost == 1)
            && p.HandZone.Count == 0, "exact two one-Mana Bananas go to opponent");
        var target = Summon(p.Opponent, "CS2_182"); End(g); PlayEntity(g, p.Opponent, p.Opponent.HandZone[0], target);
        Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 6 && target.AppliedEnchantments.Any(e => e.Card.Id == "EX1_014te"), "generated Banana gives exact +1/+1 enchantment");
        End(g); PoolPilotProof.EmptyHand(p.Opponent); PoolPilotProof.FillHand(p.Opponent, 9);
        Play(g, p, id); Core2026Proof.Check(p.Opponent.HandZone.Count == 10 && p.Opponent.HandZone.Count(c => c.Card.Id == "EX1_014t") == 1, "one free slot gets only one Banana");
        PoolPilotProof.Resume(g, next => { Play(next, next.Player1, id); Core2026Proof.Check(next.Player2.HandZone.Count == 10, "full enemy hand cannot grow"); });
    }, legacyPoolMember: true);
}
