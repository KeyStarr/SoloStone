using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class DireWolfAlphaProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_162", (g, p, id) => {
        var far = Summon(p, "CS2_182"); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); var source = PlayAt(g, p, id, 2);
        Core2026Proof.Check(left.AttackDamage == 5 && right.AttackDamage == 5 && far.AttackDamage == 4 && source.AttackDamage == 2 && enemy.AttackDamage == 4, "only immediate friendly neighbours buffed");
        Kill(g, left); Core2026Proof.Check(far.AttackDamage == 5 && right.AttackDamage == 5, "adjacency updates after neighbour death");
        PoolPilotProof.Resume(g, next => { ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[far.Id]).AttackDamage == 4 && ((Minion)next.IdEntityDic[right.Id]).AttackDamage == 4, "Silence removes aura in clone/load/original"); });
        Kill(g, source); source = PlayAt(g, p, id, 1); Kill(g, source);
        Core2026Proof.Check(far.AttackDamage == 4 && right.AttackDamage == 4, "source death removes aura");
        ParkBoard(p); LocationProofs.Location(p); right = Summon(p, "CS2_182"); source = PlayAt(g, p, id, 0);
        Core2026Proof.Check(right.AttackDamage == 4, "location blocks adjacency");
        source = PlayAt(g, p, id, 2); Core2026Proof.Check(right.AttackDamage == 5, "minion across location buffs its own actual neighbour");
        Play(g, p, "EX1_049", source); Core2026Proof.Check(right.AttackDamage == 4, "source bounce removes aura");
    }, legacyPoolMember: true);
}
