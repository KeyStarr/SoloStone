using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class StormwindChampionProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_222", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 7 && friend.AttackDamage == 5 && friend.Health == 6 && enemy.AttackDamage == 4 && enemy.Health == 5, "aura buffs only other friendly minions");
        var later = Summon(p, "CS2_182"); Core2026Proof.Check(later.AttackDamage == 5 && later.Health == 6, "later summons gain aura");
        PoolPilotProof.Resume(g, other => {
            ((Minion)other.IdEntityDic[source.Id]).Silence(); other.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)other.IdEntityDic[friend.Id]).AttackDamage == 4 && ((Minion)other.IdEntityDic[later.Id]).Health == 5, "Silence removes aura in original, clone and snapshot");
    });
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5 && later.Health == 5, "source death removes aura");
    }, legacyPoolMember: true);
}
