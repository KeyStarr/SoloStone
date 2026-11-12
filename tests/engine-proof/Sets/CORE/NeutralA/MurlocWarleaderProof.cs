using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class MurlocWarleaderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_507", (g, p, id) => {
        var friend = Summon(p, "EX1_506a"); var plain = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "EX1_506a");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && friend.AttackDamage == 3 && enemy.AttackDamage == 1
            && friend.Health == 1 && source.Health == 3 && plain.AttackDamage == 4, "aura affects only other eligible friendly minions");
        var later = Summon(p, "EX1_506a"); Core2026Proof.Check(later.AttackDamage == 3, "later eligible summons gain aura");
        PoolPilotProof.Resume(g, next => {
            ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 1 && ((Minion)next.IdEntityDic[later.Id]).AttackDamage == 1, "source Silence removes aura in original/clone/load");
        });
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(friend.AttackDamage == 1 && later.AttackDamage == 1, "source death removes aura");
        source = (Minion)Play(g, p, id); Play(g, p, "EX1_049", source);
        Core2026Proof.Check(friend.AttackDamage == 1 && later.AttackDamage == 1, "source bounce removes aura");
    }, legacyPoolMember: true);
}
