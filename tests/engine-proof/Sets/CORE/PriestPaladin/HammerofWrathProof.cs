using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class HammerofWrathProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_094", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); int deck = p.DeckZone.Count;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "three damage and one draw");
        Play(g, p, id, p.Hero); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3, "either hero target allowed");
        var friend = Summon(p, "CS2_182"); Summon(p, "CS2_142"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.Damage == 4, "friendly minion and Spell Damage supported");
        PoolPilotProof.EmptyHand(p); var source = Add(p, id); PoolPilotProof.FillHand(p);
        deck = p.DeckZone.Count; PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "lethal damage still draws into freed hand slot");
        deck = p.DeckZone.Count;
        PoolPilotProof.Resolve(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "already-full hand burns draw while effect still resolves");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 4 && p.Hero.Fatigue == 1, "empty deck causes one fatigue draw");
    }, legacyPoolMember: true);
}
