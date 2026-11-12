using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class BashProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AT_064", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); int health = p.Hero.Health;
        Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 3 && p.Hero.Armor == 3 && p.Hero.Health == health, "three damage and three Armor");
        Summon(p, "CS2_142"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.Hero.Armor == 6 && p.Opponent.Hero.Armor == 0, "Spell Damage scales damage only; enemy hero allowed");
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Armor == 5 && p.Hero.Health == health, "friendly hero allowed; damage precedes Armor");
    }, legacyPoolMember: true);
}
