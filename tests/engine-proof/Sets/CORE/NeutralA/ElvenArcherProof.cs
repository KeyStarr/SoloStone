using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class ElvenArcherProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_189", (g, p, id) => {
        Summon(p, "CS2_142"); var target = Summon(p, "CS2_182");
        var source = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source?.Id == source.Id && t.Target?.Id == source.Id), "Battlecry cannot target self");
        PlayEntity(g, p, source, target); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(target.Damage == 1 && p.Opponent.Hero.Damage == 1, "one damage to friendly minion or enemy hero; Spell Damage does not scale Battlecry");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 1, "friendly hero target allowed");
    }, legacyPoolMember: true);
}
