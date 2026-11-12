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
internal static class LightningStormProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_259", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "GVG_085");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && friend.Damage == 0 && shield.Damage == 0 && !shield.HasDivineShield && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0 && p.OverloadOwed == 1,
            "fixed three to enemy minions only; shield prevents its hit; one Overload");
        Summon(p, "CS2_142"); var large = Summon(p.Opponent, "CS2_200"); Play(g, p, id);
        Core2026Proof.Check(large.Damage == 4 && friend.Damage == 0 && p.OverloadOwed == 2, "Spell Damage applies without changing sides or Overload");
        ParkBoard(p.Opponent); Play(g, p, id); Core2026Proof.Check(p.OverloadOwed == 3, "empty enemy board is legal and still incurs Overload");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 3 && p.OverloadOwed == 0 && p.Opponent.OverloadLocked == 0, "caster alone locks mana next turn");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 0, "mana unlocked following turn");
    }, legacyPoolMember: true);
}
