using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class AnnoyOTronProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GVG_085", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2 && source.HasTaunt && source.HasDivineShield && source.IsRace(Race.MECHANICAL), "printed 1/2 Taunt Divine Shield Mech");
        Play(g, p, "CS2_189", source); Core2026Proof.Check(!source.HasDivineShield && source.Damage == 0, "first hit consumes shield without damage");
        Play(g, p, "CS2_189", source); Core2026Proof.Check(source.Health == 1, "next hit deals damage");
        source = Summon(p, id); source.Silence();
        Core2026Proof.Check(!source.HasDivineShield && !source.HasTaunt && source.IsRace(Race.MECHANICAL), "Silence removes both keywords but retains Mech");
    }, legacyPoolMember: true);
}
