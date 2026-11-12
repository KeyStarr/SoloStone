using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class WorgenInfiltratorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_010", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.HasStealth, "printed Stealth active"); End(g);
        var spell = Add(p.Opponent, "CS2_029"); var attacker = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false;
        Core2026Proof.Check(!p.Opponent.Options().Any(t => (t.Source?.Id == spell.Id || t.Source?.Id == attacker.Id || t.PlayerTaskType == PlayerTaskType.HERO_POWER)
            && t.Target?.Id == source.Id), "Stealth blocks enemy spells, attacks and hero power"); End(g);
        PoolPilotProof.Resume(g, next => {
            var minion = (Minion)next.IdEntityDic[source.Id]; Attack(next, minion, next.Player2.Hero);
            Core2026Proof.Check(!minion.HasStealth, "own attack removes Stealth"); End(next);
            Core2026Proof.Check(next.Player2.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == minion.Id), "revealed minion becomes targetable");
        });
        End(g); source = Summon(p, id); source.Silence(); Core2026Proof.Check(!source.HasStealth, "Silence removes Stealth");
    }, legacyPoolMember: true);
}
