using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class TheBlackKnightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_002", (g, p, id) => {
        var friendly = Summon(p, "CS2_179"); var enemy = Summon(p.Opponent, "CS2_179"); var plain = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id); var plays = p.Options().OfType<PlayCardTask>().Where(t => t.Source == source).ToArray();
        Core2026Proof.Check(plays.Length > 0 && plays.All(t => t.Target == enemy), "only enemy Taunt legal; friendly Taunt, plain minions and heroes excluded");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Zone == p.BoardZone && plain.Zone == p.Opponent.BoardZone, "destroy only chosen enemy Taunt");
        Play(g, p, id); CoreTradeProof.Verify(g, p, id);
    });
}
