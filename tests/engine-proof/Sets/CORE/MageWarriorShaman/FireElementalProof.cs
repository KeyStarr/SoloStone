using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class FireElementalProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_042", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); Summon(p, "CS2_142");
        Play(g, p, id, friend); Play(g, p, id, enemy);
        Core2026Proof.Check(friend.Damage == 4 && enemy.Damage == 4, "Battlecry deals four to either side, unaffected by Spell Damage");
        Play(g, p, id, p.Opponent.Hero); Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 4, "both heroes legal targets");
        var required = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == required.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Target == null), "available characters require a Battlecry target");
    }, legacyPoolMember: true);
}
