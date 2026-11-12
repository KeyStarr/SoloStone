using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class AzsharasTriumphProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_136", (g, p, id) => {
        var originalIds = p.DeckZone.Select(c => c.Id).ToArray();
        Resume(g, next => Play(next, next.Player1, id));
        var source = p.GraveyardZone.Last(c => c.Card.Id == id);
        var generated = p.DeckZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        CataclysmPoolProof.Generated(g, id, source, generated, 5, Zone.DECK, doubled: true);
        Core2026Proof.Check(generated.All(c => c.Card.Type == CardType.MINION && c.Card.Cost >= 8), "five minions cost at least eight");
        Core2026Proof.Check(originalIds.All(x => p.DeckZone.Any(c => c.Id == x && ((Minion)c).AttackDamage == 4 && ((Minion)c).Health == 5)), "existing deck minions unchanged");
        Core2026Proof.Check(p.Opponent.DeckZone.Count == 26, "opponent deck unchanged");
        var minion = (Minion)generated[0]; p.HandZone.Add(p.DeckZone.Remove(minion));
        Core2026Proof.Check(minion.AppliedEnchantments.Single(e => e.Card.Id == "CATA_136e").Card.AssetId == 122985, "exact double-stats enchantment");
        p.HandZone.Remove(minion); Generic.SummonBlock(g, minion, -1, source);
        Core2026Proof.Check(minion.AttackDamage >= minion.Card.ATK * 2 && minion.Health == minion.Card.Health * 2, "double stats survive deck/hand/play movement");
        minion.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.Health == minion.Card.Health, "Silence removes double stats");
        CataclysmPoolProof.PutBoardAside(p); EmptyHand(p);
        CataclysmPoolProof.WithPool(g, id, 0, () => {
            int count = p.DeckZone.Count; var rng = g.Random.CaptureState(); Resolve(g, source);
            Core2026Proof.Check(p.DeckZone.Count == count && rng.Equals(g.Random.CaptureState()), "empty pool does not shuffle or consume RNG");
        });
        ClassDeck(p);
        CataclysmPoolProof.WithPool(g, id, 1, () => {
            Resolve(g, source);
            CataclysmPoolProof.Generated(g, id, source, p.DeckZone, 5, Zone.DECK, doubled: true);
            Core2026Proof.Check(p.DeckZone.Select(c => c.Card.Id).Distinct().Count() == 1, "five independent picks allow duplicates");
        });
        ClassDeck(p);
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) Generic.ShuffleIntoDeck(p, null, Entity.FromCard(p, Cards.FromId("CS2_182")));
        Resolve(g, source);
        Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity && p.DeckZone.Count(c => c[GameTag.DISPLAYED_CREATOR] == source.Id) == 1, "deck capacity permits only final slot");
        Resolve(g, source);
        Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "full deck never overflows");
        FillHand(p);
        ClassDeck(p); Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 5, "full hand does not block deck generation");
    });
}
