using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using static StandardSetsProof;
internal static class SpelunkerProof
{
    internal static void Run() => Case("TLC_450", (g, p, id) => {
        var source = Core2026Proof.Play(g, p, id);
        Core2026Proof.Check(source[GameTag.ATK] == 2 && source[GameTag.HEALTH] == 3, "2/3 Battlecry");
        Core2026Proof.Check(p.AppliedEnchantments.Single().Card.AssetId == 117891, "exact player discount actor");
        var card = TemporaryRulesProof.Grant(p);
        Core2026Proof.Check(card.Cost == 2 && card[TemporaryRules.Temporary] == 1, "Unpinned_TemporaryProjection Unpinned_SpelunkerNext discount after insertion");
        Core2026Proof.Check(g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == card.Id && t.Tag == GameTag.COST && t.Value == 2), "history publishes admitted discounted COST");
        Core2026Proof.Check(Generic.PayPhase(p, card) && p.AppliedEnchantments.Count == 0, "commit consumes exact discount actor");
        TemporaryRulesProof.End(g);
        Core2026Proof.Check(card.Zone == p.GraveyardZone && p.DiscardedEntities.Contains(card.Id), "Unpinned_TemporaryExpiry real discard");
        Core2026Proof.Check(!TemporaryRules.IsTemporary(card), "Unpinned_TemporaryResidence hand exit clears marker");
        // Unpinned_TemporaryEndOrder is exercised by the shared primitive end-created fixture.
    });
}
