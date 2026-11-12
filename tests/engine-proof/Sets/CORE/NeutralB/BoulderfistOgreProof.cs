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
internal static class BoulderfistOgreProof
{
    internal static void Run() => StandardSetsProof.Case("Core_CS2_200", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 7 && source.Card.Text == "" && source.Card.Races.Count == 0, "printed vanilla 6-Mana 6/7");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id), "ordinary summon cannot attack immediately");
        End(g); End(g);
        var attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack) && p.Opponent.Hero.Damage == 6, "next friendly turn attacks for six");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 7, "Silence preserves vanilla stats");
    }, legacyPoolMember: true);
}
