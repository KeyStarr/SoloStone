using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamPoolProof;

internal static class EnvoyoftheGladeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_873", (g, p, id) => {
        PoolPilotProof.ClassDeck(p); PoolPilotProof.ClassDeck(p.Opponent);
        var source = Summon(p, id);
        var rng = g.Random.CaptureState(); PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(p.DeckZone.Count == 0 && g.Random.CaptureState().Equals(rng), "empty deck has no RNG");
        var ids = new[] { "CS2_182", "CS2_029", "CS2_106", "CS2_171", "EDR_892" };
        foreach (string cardId in ids) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(cardId)));
        var old = p.DeckZone.ToArray(); var originalIds = old.Select(c => c.Id).ToArray();
        old[0].Cost = 0;
        p.Opponent.DeckZone.Add(Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")));
        var hand = Add(p, "CS2_182"); var board = Summon(p, "CS2_182");
        WithPool(g, id, 0, () => {
            rng = g.Random.CaptureState(); PoolPilotProof.Resolve(g, source);
            Core2026Proof.Check(p.DeckZone.Select(c => c.Card.Id).SequenceEqual(ids) && g.Random.CaptureState().Equals(rng), "empty pool preserves neutral originals");
        });
        PoolPilotProof.Resume(g, next => PoolPilotProof.Resolve(next, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.DeckZone.Select(c => c.Id).SequenceEqual(originalIds), "deck size/order/entity ids preserved");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        foreach (int position in new[] { 0, 3 })
        {
            var card = p.DeckZone[position];
            Core2026Proof.Check(candidates.Any(c => c.Id == card.Card.Id) && card.Card.Id != id && card.Controller == p
                && card.Zone == p.DeckZone && card.ZonePosition == 0 && g.IdEntityDic[card.Id] == card, "neutral transformed into declared Druid member in place; deck positions hidden");
            var member = g.OfflinePools.Standard.Single(m => m.CardId == card.Card.Id);
            Core2026Proof.Check(member.Classes.Contains("DRUID") && card.Card.Cost == member.Cost && card.Card.ATK == member.Attack
                && card.Card.Health == member.Health && card.Cost == card.Card.Cost && card[GameTag.COST] == card.Cost, "pinned Druid stats and reset visible cost");
        }
        Core2026Proof.Check(p.DeckZone[1] == old[1] && p.DeckZone[2] == old[2] && p.DeckZone[4] == old[4]
            && p.DeckZone[1].Card.Id == "CS2_029" && p.DeckZone[4].Card.Id == "EDR_892", "all non-neutral classes preserved");
        Core2026Proof.Check(p.Opponent.DeckZone[0].Card.Id == "CS2_182" && hand.Card.Id == "CS2_182" && board.Card.Id == "CS2_182", "other deck, hand and board unaffected");
        PoolPilotProof.ClassDeck(p);
        foreach (int size in new[] { 1, 2 }) WithPool(g, id, size, () => {
            for (int i = 0; i < 3; i++) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
            PoolPilotProof.FillHand(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            long draws = g.Random.DrawCount; PoolPilotProof.Resolve(g, source);
            Core2026Proof.Check(p.DeckZone.Count == 3 && p.DeckZone.All(c => c.Card.Class == CardClass.DRUID)
                && g.Random.DrawCount == draws + 3 && p.HandZone.Count == 10 && p.BoardZone.SlotCount == 7, "tiny pool samples independently despite full hand/board");
            PoolPilotProof.ClassDeck(p);
        });
    });
}
