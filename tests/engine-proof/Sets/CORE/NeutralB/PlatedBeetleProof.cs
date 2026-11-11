using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class PlatedBeetleProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_413", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.IsRace(Race.BEAST), "printed Beast");
        PoolPilotProof.Resume(g, next => {
            Kill(next,(Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.Hero.Armor == 3 && next.Player2.Hero.Armor == 0, "death grants three Armor to owner across clone/snapshot");
    });
        source = (Minion)Play(g,p,id); source.Silence(); Kill(g,source); Core2026Proof.Check(p.Hero.Armor == 3,"silenced death grants nothing");
        source = Summon(p.Opponent,id); Kill(g,source); Core2026Proof.Check(p.Hero.Armor == 3 && p.Opponent.Hero.Armor == 3,"opponent death credits opponent only");
    }, legacyPoolMember: true);
}
