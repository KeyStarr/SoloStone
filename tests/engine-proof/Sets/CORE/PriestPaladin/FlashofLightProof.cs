using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class FlashofLightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TRL_307", (g, p, id) => {
        p.Hero.Damage = 9; p.Opponent.Hero.Damage = 9; int deck = p.DeckZone.Count;
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 5 && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "heal four and draw one");
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "enemy hero can be healed");
        var friend = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200"); friend.Damage = 6; enemy.Damage = 2;
        Play(g, p, id, friend); Play(g, p, id, enemy);
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 0, "either minion target allowed, healing capped");
        PoolPilotProof.EmptyHand(p); var source = Add(p, id); PoolPilotProof.FillHand(p);
        deck = p.DeckZone.Count; PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Health == 7 && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "undamaged target still draws into freed slot");
        deck = p.DeckZone.Count;
        PoolPilotProof.Resolve(g, source, enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "already-full hand burns draw while effect still resolves");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 2 && p.Hero.Fatigue == 1, "heal resolves before one empty-deck fatigue draw");
    }, legacyPoolMember: true);
}
