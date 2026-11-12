using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class FrostboltProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_024", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 3 && target.IsFrozen, "three damage and Freeze");
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Hero.IsFrozen, "friendly hero is legal and frozen");
        Summon(p, "CS2_142"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.Opponent.Hero.IsFrozen, "enemy hero and Spell Damage");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && shield.IsFrozen, "shield prevents damage but not Freeze");
    }, legacyPoolMember: true);
}
