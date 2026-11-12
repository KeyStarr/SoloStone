using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier A: https://hearthstone.blizzard.com/en-us/cards/2071-micro-machine/ (checked 2026-10-21), "each turn".
// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags: each turn includes both controllers; card-local EitherTurn correction.
internal static class MicroMachineProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GVG_103", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2 && source.IsRace(Race.MECHANICAL), "printed 1/2 Mech");
        PoolPilotProof.Resume(g, next => {
            End(next);
            var machine = (Minion)next.IdEntityDic[source.Id];
            Core2026Proof.Check(machine.AttackDamage == 2 && machine.Health == 2, "opponent turn start grants +1 Attack");
        });
        End(g); Core2026Proof.Check(source.AttackDamage == 3 && source.AppliedEnchantments.Any(e => e.Card.Id == "GVG_076a"), "own turn also grants exact enchantment");
        source.Silence(); End(g); End(g);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2, "Silence removes gains and both turn listeners");
        var departed = Summon(p, id); Kill(g, departed); End(g); End(g);
        Core2026Proof.Check(departed.AttackDamage == 1 && departed.Zone == p.GraveyardZone, "departed source no longer grows");
    }, legacyPoolMember: true);
}
