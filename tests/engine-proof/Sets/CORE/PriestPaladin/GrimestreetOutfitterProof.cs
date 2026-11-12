using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class GrimestreetOutfitterProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_753", (g, p, id) => {
        var first = (Minion)Add(p, "CS2_182"); var second = (Minion)Add(p, "CS2_200");
        var spell = Add(p, "CS2_029"); var weapon = Add(p, "CS2_091");
        var enemyHand = (Minion)Add(p.Opponent, "CS2_182");
        var board = Summon(p, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.AttackDamage == 5 && first.Health == 6 && second.AttackDamage == 7 && second.Health == 8, "all friendly hand minions gain +1/+1");
        Core2026Proof.Check(first.AppliedEnchantments.Single().Card.Id == "CFM_753e", "exact Smuggling enchantment");
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2 && board.AttackDamage == 4 && board.Health == 5 && enemyHand.AttackDamage == 4 && enemyHand.Health == 5,
            "source, board and enemy hand excluded");
        Core2026Proof.Check(spell.Cost == 4 && weapon.Cost == 1 && (spell.AppliedEnchantments?.Count ?? 0) == 0 && (weapon.AppliedEnchantments?.Count ?? 0) == 0, "non-minions excluded");
        PlayEntity(g, p, first); Kill(g, source);
        Core2026Proof.Check(first.AttackDamage == 5 && first.Health == 6, "hand buff persists after play and source death");
        first.Silence(); Core2026Proof.Check(first.AttackDamage == 4 && first.Health == 5, "Silence removes hand buff");
        PoolPilotProof.EmptyHand(p); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "empty hand battlecry creates nothing");
    }, legacyPoolMember: true);
}
