using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class LorewalkerChoProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_100", (g, p, id) => {
        var source = Summon(p, id);
        // First cast must happen in the clone before the original trigger has ever run.
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, "GAME_005"));
        Core2026Proof.Check(p.Opponent.HandZone.Single().Card.Id == "GAME_005" && p.HandZone.Count == 0, "friendly cast copies exact spell to opponent, no recursion");
        End(g); int hand = p.Opponent.HandZone.Count; PlayEntity(g, p.Opponent, p.Opponent.HandZone.Single(c => c.Card.Id == "GAME_005"));
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "GAME_005" && p.Opponent.HandZone.Count == hand - 1, "enemy cast copies exact spell back to owner");
        PoolPilotProof.EmptyHand(p.Opponent); End(g);
        PoolPilotProof.EmptyHand(p); Play(g, p, "CS2_182"); Core2026Proof.Check(p.Opponent.HandZone.Count == 0, "minion play does not copy");
        PoolPilotProof.Resume(g, next => { Play(next, next.Player1, "CS2_008", next.Player2.Hero);
            Core2026Proof.Check(next.Player2.HandZone.Single().Card.Id == "CS2_008" && next.Player2.Hero.Damage == 1, "targeted spell resolves once and copies exact id in clone/load/original"); });
        PoolPilotProof.FillHand(p.Opponent); Play(g, p, "GAME_005"); Core2026Proof.Check(p.Opponent.HandZone.Count == 10, "copy cannot overflow full receiving hand");
        PoolPilotProof.EmptyHand(p.Opponent); source.Silence(); Play(g, p, "GAME_005"); Core2026Proof.Check(p.Opponent.HandZone.Count == 0, "Silence removes copying trigger");
        Kill(g, source); source = Summon(p, id); Play(g, p, "EX1_049", source);
        PoolPilotProof.EmptyHand(p.Opponent); Play(g, p, "GAME_005"); Core2026Proof.Check(p.Opponent.HandZone.Count == 0, "departed source no longer copies spells");
    }, legacyPoolMember: true);
}
