using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class ShadowAscendantProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ICC_210", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.IsRace(Race.UNDEAD), "current Undead tribe");
        End(g);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3 && enemy.AttackDamage == 4 && enemy.Health == 5, "cannot buff self or enemy when alone");
        End(g);
        var friend = Summon(p, "CS2_182");
        PoolPilotProof.Resume(g, next => {
            End(next);
            var target = (Minion)next.IdEntityDic[friend.Id];
            Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 6 && target.AppliedEnchantments.Single().Card.Id == "ICC_210e", "owner turn end grants exact +1/+1 buff across clone/snapshot");
        });
        End(g); Core2026Proof.Check(friend.AttackDamage == 5 && enemy.AttackDamage == 4, "opponent turn end does not trigger either side");
        source.Silence(); End(g);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6, "Silence stops future triggers and leaves granted buff");
        End(g); Kill(g, source); var second = (Minion)Play(g, p, id); Kill(g, second); End(g);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6, "departed source does not trigger");
        End(g); var active = (Minion)Play(g, p, id); var other = Summon(p, "CS2_182");
        End(g);
        Core2026Proof.Check((friend.AttackDamage == 6 && friend.Health == 7 && other.AttackDamage == 4 && other.Health == 5)
            || (friend.AttackDamage == 5 && friend.Health == 6 && other.AttackDamage == 5 && other.Health == 6), "exactly one of two other friendly minions gains +1/+1");
        Core2026Proof.Check(active.AttackDamage == 2 && active.Health == 3, "random buff still excludes its source with multiple candidates");
        friend.Silence(); Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5, "Silence removes recipient buff");
    }, legacyPoolMember: true);
}
