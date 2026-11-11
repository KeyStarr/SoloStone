using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class GrimHarvestProof
{
    // misc-cards/dreadseed-token-recipes.yaml PINNED-RULE-ONLY (A/C/D exact recipes);
    // dreadseed-roll-replacement.yaml STILL-UNPINNED: owner assumes independent uniform rolls with replacement.
    internal static void Run() => StandardSetsProof.Case("EDR_840", Unpinned_GrimHarvest);
    private static void Unpinned_GrimHarvest(Game g, Controller p, string id) {
        for (int seed = 1; seed <= 3; seed++)
        {
            ParkBoard(p); EmptyHand(p); g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            string expected = DreadseedProof.Ids[oracle.Random.Next(3)];
            var spell = Play(g, p, id);
            Core2026Proof.Check(BEPoolProof.Children(p, spell).Single().Card.Id == expected,
                "Unpinned_GrimHarvest: exact seeded token, not always first");
        }
        ParkBoard(p); EmptyHand(p);

        DreadseedProof.Closure(); DreadseedProof.HoundAndCrow(g, p);
        DreadseedProof.Outcomes(g, p, () => {
            int hand = p.HandZone.Count, deck = p.DeckZone.Count;
            var spell = Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1, "Grim Harvest draws one card");
            var children = BEPoolProof.Children(p, spell);
            Core2026Proof.Check(g.HistoryFacts.Query.HandEntries().Last().Sequence
                < g.HistoryFacts.Query.Summons().Single(f => f.EntityId == children.Single().Id).Sequence, "draw precedes Dreadseed summon");
            return children;
        }, 1);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int count = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == count - 1 && p.HandZone.Count == 1 && p.BoardZone.Count == 7,
            "full board still draws without summoning");
    }
}
