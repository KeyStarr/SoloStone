using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class HolySmiteProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS1_130", (g, p, id) => {
        var friend = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200");
        TargetRules(g, p, id, enemy, friend, false);
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 3, "three damage");
        Summon(p, "CS2_142"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.Damage == 4 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "four damage with Spell Damage, heroes untouched");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "shield absorbs damage");
    }, legacyPoolMember: true);
}
