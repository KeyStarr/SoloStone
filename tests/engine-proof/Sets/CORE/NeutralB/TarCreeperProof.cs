using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class TarCreeperProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_928", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, id);
        Core2026Proof.Check(source.AttackDamage == 1 && enemy.AttackDamage == 3 && source.Health == 5 && source.HasTaunt, "1/5 on own turn; enemy copy gains two on this turn");
        PoolPilotProof.Resume(g, next => {
            End(next); Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 3 && ((Minion)next.IdEntityDic[enemy.Id]).AttackDamage == 1,
                "turn switch exchanges +2 Attack in original/clone/snapshot");
        });
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 1 && !source.HasTaunt, "Silence removes conditional Attack and Taunt");
        End(g); Core2026Proof.Check(source.AttackDamage == 1 && enemy.AttackDamage == 3, "next switch does not reactivate silenced aura");
        var departed = Summon(p, id); Kill(g, departed); End(g);
        Core2026Proof.Check(departed.AttackDamage == 1 && departed.Zone == p.GraveyardZone, "source death removes turn-dependent aura");
    }, legacyPoolMember: true);
}
