using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class RighteousProtectorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ICC_038", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var friend = Summon(p, "CS2_182");
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 1 && source.HasTaunt && source.HasDivineShield, "printed 1/1 Taunt and Divine Shield");
        End(g); var attacker = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false;
        var options = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id).ToArray();
        Core2026Proof.Check(options.Length == 1 && options[0].Target?.Id == source.Id, "Taunt blocks hero and other minion attacks");
        Attack(g, attacker, source); Core2026Proof.Check(source.Health == 1 && !source.HasDivineShield, "first hit loses shield without damage");
        source.Silence();
        Core2026Proof.Check(!source.HasTaunt && !source.HasDivineShield && source.AttackDamage == 1 && source.Health == 1, "Silence removes keywords only");
        attacker.IsExhausted = false;
        Core2026Proof.Check(p.Opponent.Options().Any(t => t.Source?.Id == attacker.Id && t.Target?.Id == p.Hero.Id), "hero target available after Taunt removed");
    }, legacyPoolMember: true);
}
