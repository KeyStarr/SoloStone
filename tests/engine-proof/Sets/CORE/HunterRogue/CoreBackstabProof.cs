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
internal static class CoreBackstabProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_072", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        var damaged = Summon(p.Opponent, "CS2_182"); damaged.Damage = 1;
        var source = Add(p, id); var options = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == enemy) && options.Any(t => t.Target == friendly)
            && options.All(t => t.Target is Minion && t.Target != damaged), "either side undamaged minions only; damaged minions and heroes excluded");
        PlayEntity(g, p, source, enemy); Core2026Proof.Check(enemy.Damage == 2 && damaged.Damage == 1, "two damage to chosen undamaged minion");
        Summon(p, "CS2_142"); Play(g, p, id, friendly); Core2026Proof.Check(friendly.Damage == 3, "Spell Damage applies");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "Divine Shield absorbs damage");
    }, legacyPoolMember: true);
}
