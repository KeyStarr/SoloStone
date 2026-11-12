using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class GreaterHealingPotionProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_604", (g, p, id) => {
        p.Hero.Damage = 20; p.Opponent.Hero.Damage = 10;
        var source = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.Target?.Controller == p), "enemy characters excluded");
        int deck = p.DeckZone.Count; PlayEntity(g, p, source, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 8 && p.Opponent.Hero.Damage == 10 && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "heal twelve and draw exactly one");
        var friend = Summon(p, "CS2_200"); friend.Damage = 6;
        Play(g, p, id, friend); Core2026Proof.Check(friend.Health == 7, "friendly minion capped at maximum Health");
        PoolPilotProof.EmptyHand(p); source = Add(p, id); PoolPilotProof.FillHand(p);
        deck = p.DeckZone.Count; PlayEntity(g, p, source, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "last hand slot filled by draw; healing capped");
        deck = p.DeckZone.Count;
        PoolPilotProof.Resolve(g, source, p.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "already-full hand burns draw while effect still resolves");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
        Play(g, p, id, friend); Core2026Proof.Check(p.Hero.Damage == 1 && p.Hero.Fatigue == 1, "undamaged target still draws and fatigues with empty deck");
    }, legacyPoolMember: true);
}
