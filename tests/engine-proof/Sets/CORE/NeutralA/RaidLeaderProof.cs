using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class RaidLeaderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_122", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var plain = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && friend.AttackDamage == 5 && enemy.AttackDamage == 4
            && friend.Health == 5 && source.Health == 3 && plain.AttackDamage == 5, "aura affects only other eligible friendly minions");
        var later = Summon(p, "CS2_182"); Core2026Proof.Check(later.AttackDamage == 5, "later eligible summons gain aura");
        PoolPilotProof.Resume(g, next => {
            ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 4 && ((Minion)next.IdEntityDic[later.Id]).AttackDamage == 4, "source Silence removes aura in original/clone/load");
        });
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(friend.AttackDamage == 4 && later.AttackDamage == 4, "source death removes aura");
        source = (Minion)Play(g, p, id); Play(g, p, "EX1_049", source);
        Core2026Proof.Check(friend.AttackDamage == 4 && later.AttackDamage == 4, "source bounce removes aura");
    }, legacyPoolMember: true);
}
