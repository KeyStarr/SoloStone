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
internal static class LightningBoltProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_238", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, friend); Play(g, p, id, enemy);
        Core2026Proof.Check(friend.Damage == 3 && enemy.Damage == 3 && p.OverloadOwed == 2, "three damage to either minion, one Overload per cast");
        Play(g, p, id, p.Hero); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3, "both heroes legal targets");
        Summon(p, "CS2_142"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 7 && p.OverloadOwed == 5, "Spell Damage increases damage, never Overload");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && !shield.HasDivineShield && p.OverloadOwed == 6, "prevented damage still owes Overload");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 6 && p.OverloadOwed == 0 && p.Opponent.OverloadLocked == 0, "only caster locks owed mana next turn");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 0, "Overload expires following turn");
    }, legacyPoolMember: true);
}
