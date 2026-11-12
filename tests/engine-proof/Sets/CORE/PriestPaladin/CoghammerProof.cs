using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class CoghammerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GVG_059", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 2 && p.Hero.Weapon.Durability == 3 && !enemy.HasTaunt && !enemy.HasDivineShield, "empty friendly board still equips 2/3 without buffing enemy");
        var first = Summon(p, "CS2_182"); var second = Summon(p, "CS2_200");
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, id);
            var friends = next.Player1.BoardZone.ToArray();
            Core2026Proof.Check(friends.Count(m => m.HasTaunt && m.HasDivineShield) == 1 && friends.Count(m => !m.HasTaunt && !m.HasDivineShield) == 1,
                "one random friendly minion receives both keywords, clone resumes same RNG");
        });
        var chosen = p.BoardZone.Single(m => m.HasTaunt);
        Play(g, p, "CS2_091"); Core2026Proof.Check(chosen.HasTaunt && chosen.HasDivineShield, "buff survives weapon leaving play");
        chosen.Silence(); Core2026Proof.Check(!chosen.HasTaunt && !chosen.HasDivineShield, "Silence removes both granted keywords");
    }, legacyPoolMember: true);
}
