using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class WantedPosterProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_407", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        PoolPilotProof.Resume(replay, next => {
            // Neutral spells are excluded from Tess; exercise the indirect cast entry point directly.
            var spell = (Spell)Entity.FromCard(next.Player1, Cards.FromId(id));
            SabberStoneCore.Actions.Generic.CastSpell(next.Player1, next, spell, null, 0);
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " indirect replay finishes its recipe synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " indirect replay completes cast once");
            var kept = next.Player1.HandZone.Single();
            Core2026Proof.Check(kept.Card.Type == CardType.MINION && kept.Card.Cost >= 5 && kept[GameTag.PREPARE] == 1
                && kept[GameTag.DECK_ACTION_COST] == 1, "Indirect Poster grants selected minion with Prepare");
            Core2026Proof.End(next);
        });

        DecisionSpellOrderingProof.Run(g.Seed, id, (next, chosen) =>
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone && chosen[GameTag.PREPARE] == 1
                && chosen[GameTag.DECK_ACTION_COST] == 1, "Poster Discover and Prepare complete before after-cast"));

        var expected = OfflineCardPools.SampleDistinct(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId);
        var source = Play(g, p, id);
        Offers(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Choice.Choices.Select(n => g.IdEntityDic[n].Card.Id).SequenceEqual(expected.Select(c => c.Id)), "Discover seeded order, no early hand result");
        EachAnswer(g, (branch, selected) => {
            var chosen = branch.IdEntityDic[selected];
            Core2026Proof.Check(chosen.Zone == branch.Player1.HandZone && chosen.ZonePosition == 0
                && chosen[GameTag.PREPARE] == 1 && chosen[GameTag.DECK_ACTION_COST] == 1
                && (chosen.Card[GameTag.PREPARE] != 0 || chosen.AppliedEnchantments.Any(e => e.Card.Id == "CAP_407e")), "each offer gains exact Prepare grant");
            Core2026Proof.Check(branch.Player1.Options().Any(t => t is PrepareCardTask && t.Source.Id == selected), "bot/player Prepare action available");
        });
        var result = p.HandZone.Single();
        int expectedCost = result.Card.Id == "JAIL_204" && p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0
            ? 2 : Math.Max(0, result.Cost - SabberStoneCore.Actions.PrepareRules.ManaToSpend(result) - 1);
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, result)) && result[GameTag.PREPARING] == 1
            && result[GameTag.COST] == expectedCost, $"granted Prepare spends mana, publishes discount and locks card: {result.Card.Id} expected={expectedCost} actual={result.Cost} tag={result[GameTag.COST]} preparing={result[GameTag.PREPARING]}");
        Resume(g, next => End(next));
        Core2026Proof.Check(result[GameTag.PREPARING] == 0 && result[GameTag.PREPARE] == 1, "turn expiry clears lock, preserves granted ability");
        End(g);
        foreach (int count in new[] { 0, 1, 2 }) WithPool(g, id, count, () => {
            EmptyHand(p); var rng = g.Random.DrawCount; Play(g, p, id);
            Core2026Proof.Check(g.Random.DrawCount == rng, "tiny Discover consumes no RNG");
            if (count == 0) Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "empty pool no stale grant");
            else { Offers(g, g.IdEntityDic[p.Choice.SourceId], count); Choose(g); Core2026Proof.Check(p.HandZone.Single()[GameTag.PREPARE] == 1, "tiny pool still grants Prepare"); }
        });
        EmptyHand(p); Play(g, p, id); FillHand(p);
        int picked = p.Choice.Choices.First(); Choose(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[picked].Zone == p.GraveyardZone, "full hand burns selected entity");
        EmptyHand(p); var plain = Add(p, g.IdEntityDic[picked].Card.Id);
        Core2026Proof.Check(plain[GameTag.PREPARE] == plain.Card[GameTag.PREPARE], "grant never mutates shared Card");
    });
}
