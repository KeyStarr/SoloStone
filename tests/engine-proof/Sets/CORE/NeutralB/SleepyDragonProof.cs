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
internal static class SleepyDragonProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_137", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 12 && source.HasTaunt && source.IsRace(Race.DRAGON), "current 6/12 Taunt Dragon");
        End(g); var attacker = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false;
        var targets = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id).ToArray();
        Core2026Proof.Check(targets.Length == 1 && targets[0].Target?.Id == source.Id, "Taunt blocks opposing face attack");
        source.Silence(); Core2026Proof.Check(!source.HasTaunt && source.IsRace(Race.DRAGON)
            && p.Opponent.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id && t.Target?.Id == p.Hero.Id), "Silence removes Taunt restriction and retains Dragon type");
    }, legacyPoolMember: true);
}
