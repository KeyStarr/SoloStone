using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class EqualityProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_619", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_200");
        var other = Summon(p, "CS2_200"); other.Damage = 2;
        friend.Damage = 3; enemy.Damage = 5; p.Hero.Damage = 4; p.Opponent.Hero.Damage = 4;
        Play(g, p, id);
        Core2026Proof.Check(friend.Health == 1 && other.Health == 1 && enemy.Health == 1 && friend.AttackDamage == 4 && enemy.AttackDamage == 6, "both sides current Health becomes one without changing Attack");
        Core2026Proof.Check(friend.AppliedEnchantments.Single().Card.Id == "EX1_619e" && enemy.AppliedEnchantments.Single().Card.Id == "EX1_619e", "exact Equality enchantment");
        Core2026Proof.Check(p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 4, "heroes excluded");
        Play(g, p, id); Core2026Proof.Check(friend.Health == 1 && other.Health == 1 && enemy.Health == 1, "one-Health minions remain one");
        friend.Silence(); Core2026Proof.Check(friend.Health == 5, "Silence removes set-Health effect");
        ParkBoard(p); ParkBoard(p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && p.Opponent.BoardZone.SlotCount == 0, "empty board legal");
    }, legacyPoolMember: true);
}
