using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class FireballProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_029", (g, p, id) => {
        Play(g, p, id, p.Opponent.Hero); Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 6 && p.Opponent.Hero.Damage == 6, "six damage to either hero");
        var friend = Summon(p, "CS2_182"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD, "friendly minion target allowed");
        Summon(p, "CS2_142"); var target = Summon(p.Opponent, "CS2_200"); Play(g, p, id, target);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD, "seven damage with Spell Damage kills seven Health");
    }, legacyPoolMember: true);
}
