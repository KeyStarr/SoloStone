using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadowcloakedAssailantProof
{
    // PINNED count/random choice: tie-break-cards/w02-assailant-one-random.yaml (C wiki, E VS/player test).
    // Unpinned movement/reset/full-deck shuffle: tie-break-cards/w02-assailant-shuffle-wire.yaml, owner working_assumption.
    internal static void Run() => StandardSetsProof.Case("EDR_524", Unpinned_ShadowcloakedAssailant);

    private static void Unpinned_ShadowcloakedAssailant(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        Add(p, "CS2_029"); Add(p, "CS2_182");
        var first = Add(enemy, "CS2_029"); var duplicate = Add(enemy, "CS2_029");
        var second = Add(enemy, "CS2_182"); var unmatched = Add(enemy, "CS2_200");
        first.Cost = 0; duplicate.Cost = 0; second.Cost = 0;
        int deck = enemy.DeckZone.Count;
        var source = Add(p, id);
        var oldDeck = enemy.DeckZone.Select(c => c.Id).ToArray();
        long draws = g.Random.DrawCount;
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        var moved = new[] { first, duplicate, second }.Where(c => c.Zone == enemy.DeckZone).ToArray();
        Core2026Proof.Check(moved.Length == 1 && enemy.HandZone.Count == 3 && enemy.DeckZone.Count == deck + 1,
            "exactly one matching enemy hand entity shuffled, including duplicate matches");
        Core2026Proof.Check(moved[0].Controller == enemy && moved[0].Cost == moved[0].Card.Cost && unmatched.Zone == enemy.HandZone,
            "same entity/controller; ordinary hand-to-deck reset; unmatched card untouched");
        Core2026Proof.Check(!oldDeck.SequenceEqual(enemy.DeckZone.Where(c => c.Id != moved[0].Id).Select(c => c.Id)),
            "whole existing enemy deck shuffled, not merely random insertion");
        Core2026Proof.Check(g.Random.DrawCount - draws == deck + 3,
            "one random match, one insertion and exactly one shuffle of the resulting deck");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Any(c => c.Card.Id == "CS2_029") && p.HandZone.Any(c => c.Card.Id == "CS2_182"),
            "own matching hand cards retained");

        PoolPilotProof.EmptyHand(p); PoolPilotProof.EmptyHand(enemy); ParkBoard(p);
        Add(p, "CS2_029"); Add(enemy, "CS2_200");
        var rng = g.Random.CaptureState(); deck = enemy.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(enemy.DeckZone.Count == deck && enemy.HandZone.Count == 1 && rng.Equals(g.Random.CaptureState()),
            "no match: no movement, shuffle or RNG");
        PoolPilotProof.EmptyHand(enemy); Play(g, p, id);
        Core2026Proof.Check(enemy.HandZone.Count == 0 && enemy.DeckZone.Count == deck, "empty enemy hand fizzles");

        // The Battlecry card has already left hand when checking for shared identities.
        PoolPilotProof.EmptyHand(p); ParkBoard(p); Add(enemy, id);
        Play(g, p, id);
        Core2026Proof.Check(enemy.HandZone.Single().Card.Id == id, "Assailant does not match its own played source");

        PoolPilotProof.EmptyHand(p); PoolPilotProof.EmptyHand(enemy); ParkBoard(p);
        Summon(p, "LOE_077"); Add(p, "CS2_029");
        first = Add(enemy, "CS2_029"); second = Add(enemy, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(first.Zone == enemy.DeckZone && second.Zone == enemy.DeckZone && enemy.HandZone.Count == 0,
            "Brann re-evaluates live matches and shuffles one per Battlecry");

        // Movement must not remove a card if the destination deck cannot accept it.
        ParkBoard(p); Add(enemy, "CS2_029");
        while (!enemy.DeckZone.IsFull) enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_182")));
        oldDeck = enemy.DeckZone.Select(c => c.Id).ToArray(); rng = g.Random.CaptureState();
        first = enemy.HandZone.Single(); Play(g, p, id);
        Core2026Proof.Check(first.Zone == enemy.HandZone && enemy.DeckZone.Count == DeckZone.DeckMaximumCapcity, "full deck preserves the enemy hand card");
        Core2026Proof.Check(oldDeck.SequenceEqual(enemy.DeckZone.Select(c => c.Id)) && rng.Equals(g.Random.CaptureState()),
            "full deck: no shuffle or RNG");
    }
}
