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
internal static class GlacialShardProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_205", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = Add(p, id);
        var targets = p.Options().Where(t => t is PlayCardTask && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target?.Id == enemy.Id) && targets.Any(t => t.Target?.Id == p.Opponent.Hero.Id)
            && targets.All(t => t.Target?.Controller == p.Opponent), "only enemy minions and hero may be frozen");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.IsFrozen && !friend.IsFrozen && !p.Hero.IsFrozen && enemy.Damage == 0, "enemy frozen without damage");
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.IsFrozen, "enemy hero may be frozen");
        End(g); enemy.IsExhausted = false;
        Core2026Proof.Check(!p.Opponent.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id), "Freeze blocks attack during next opposing turn");
        End(g); Core2026Proof.Check(!enemy.IsFrozen && !p.Opponent.Hero.IsFrozen, "Freeze expires after missed attack turn");
    }, legacyPoolMember: true);
}
