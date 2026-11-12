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
internal static class CoreArcaneShotProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DS1_185", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        Play(g, p, id, enemy); Play(g, p, id, friendly); Play(g, p, id, p.Hero); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(enemy.Damage == 2 && friendly.Damage == 2 && p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 2, "two damage to any character on either side");
        Summon(p, "CS2_142"); Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "Spell Damage applies");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield); Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "shield absorbs damage");
    }, legacyPoolMember: true);
}
