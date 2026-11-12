using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class AbusiveSergeantProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_188", (g, p, id) => {
        Play(g, p, id); ParkBoard(p);
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); PoolPilotProof.EmptyHand(p);
        Play(g, p, id, friend); Play(g, p, id, enemy);
        Core2026Proof.Check(friend.AttackDamage == 6 && enemy.AttackDamage == 6 && friend.Health == 5 && enemy.Health == 5, "either side gains exactly two Attack only");
        Core2026Proof.Check(friend.AppliedEnchantments.Any(e => e.Card.Id == "CS2_188o"), "pinned temporary enchantment");
        PoolPilotProof.Resume(g, next => {
            End(next); Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 4 && ((Minion)next.IdEntityDic[enemy.Id]).AttackDamage == 4, "buff expires at current turn end on either side");
        });
    }, legacyPoolMember: true);
}
