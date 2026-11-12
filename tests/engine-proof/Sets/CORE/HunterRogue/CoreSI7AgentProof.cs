using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreSI7AgentProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_134", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 0 && p.Opponent.Hero.Damage == 0, "no Combo means no damage and no target required");
        Summon(p, "CS2_142"); Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 3, "Combo deals three, unaffected by Spell Damage");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 3, "Combo can target friendly hero");
        ParkBoard(p); Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "Combo targets enemy hero");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield); Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "shield prevents Combo damage");
    }, legacyPoolMember: true);
}
