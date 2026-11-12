using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class FrothingBerserkerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_604", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_008", friend); Play(g, p, "CS2_008", enemy);
        Core2026Proof.Check(source.AttackDamage == 4, "friendly and enemy minion damage both increase Attack");
        Play(g, p, "CS2_008", p.Opponent.Hero); Core2026Proof.Check(source.AttackDamage == 4, "hero damage does not trigger");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, "CS2_008", shield);
        Core2026Proof.Check(source.AttackDamage == 4, "prevented minion damage does not trigger");
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "CS2_008", (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 5, "self damage triggers across clone/snapshot");
    });
        End(g); Play(g, p.Opponent, "CS2_008", friend); Core2026Proof.Check(source.AttackDamage == 6, "buff persists and triggers on opponent turn");
        source.Silence(); Play(g, p.Opponent, "CS2_008", enemy);
        Core2026Proof.Check(source.AttackDamage == 2, "Silence removes accumulated buff and trigger");
        Kill(g, source); Play(g, p.Opponent, "CS2_008", enemy); Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD, "departed listener remains removed");
    }, legacyPoolMember: true);
}
