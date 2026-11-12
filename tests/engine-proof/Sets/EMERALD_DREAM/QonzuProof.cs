using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class QonzuProof
{
    // STILL-UNPINNED: discover/w14-qonzu-overflow.yaml; C Q'onzu Notes; owner assumption:
    // retain pending spell outside hand, answer keep/topdeck, then capacity-check its destination.
    internal static void Run() => StandardSetsProof.Case("EDR_517", Unpinned_Qonzu);

    private static void Unpinned_Qonzu(Game fixture, Controller owner, string id)
    {
        foreach (bool top in new[] { false, true })
        foreach (bool full in new[] { false, true })
        {
            var g = GiftedDiscoverProof.New(fixture.Seed, CardClass.MAGE); var p = g.Player1;
            var expected = OfflineCardPools.SampleDistinct(g.CloneExact(history: true), OfflinePoolFilter.Discover(type: CardType.SPELL),
                p.HeroClass, Cards.FromId(id).AssetId);
            var source = Play(g, p, id);
            var offers = p.Choice.Choices.Select(n => g.IdEntityDic[n]).ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Card.Id).SequenceEqual(expected.Select(c => c.Id))
                && offers.Select(c => c.Card.Id).Distinct().Count() == 3
                && offers.All(c => c.Card.Type == CardType.SPELL && c.Card.Id != id
                    && g.OfflinePools.Standard.Single(m => m.CardId == c.Card.Id).Classes.Any(v => v == "MAGE" || v == "NEUTRAL")),
                "seeded distinct Mage/neutral spell offers, source excluded");
            var selected = offers[0]; PlayerDecisionProof.Answer(g, selected.Id);
            Core2026Proof.Check(selected.Zone == p.SetasideZone && p.HandZone.Count == 0
                && p.Choice.Choices.Select(n => g.IdEntityDic[n].Card.Id).SequenceEqual(new[] { "EDR_517A", "EDR_517B" }),
                "spell stays pending until exact exported keep/topdeck options answered");
            if (full) while (!p.HandZone.IsFull) Add(p, "CS2_182");
            var order = p.Opponent.DeckZone.Select(c => c.Id).ToArray(); int hand = p.HandZone.Count;
            int option = p.Choice.Choices.Single(n => g.IdEntityDic[n].Card.Id == (top ? "EDR_517B" : "EDR_517A"));
            PoolPilotProof.Resume(g, branch => {
                var recipient = branch.Player1; var card = branch.IdEntityDic[selected.Id];
                PlayerDecisionProof.Answer(branch, option);
                Core2026Proof.Check(recipient.Choice == null && !branch.Resolution.HasSuspendedEffect, "two-step Battlecry finishes");
                if (top)
                    Core2026Proof.Check(recipient.Opponent.DeckZone.TopCard == card && card.Controller == recipient.Opponent
                        && recipient.Opponent.DeckZone.Take(order.Length).Select(c => c.Id).SequenceEqual(order)
                        && recipient.HandZone.Count == hand, "direct topdeck preserves deck order even with ten-card hand");
                else Core2026Proof.Check(card.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND)
                    && recipient.HandZone.Count == hand + (full ? 0 : 1), "keep grants once, or burns only at final full-hand delivery");
            });
        }
        {
            var g = GiftedDiscoverProof.New(fixture.Seed, CardClass.MAGE); var p = g.Player1;
            LostCityPoolProof.WithPool(g, id, Array.Empty<string>(), () => {
                var rng = g.Random.DrawCount; Play(g, p, id);
                Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && p.HandZone.Count == 0
                    && g.Random.DrawCount == rng, "empty spell pool creates neither spell choice nor keep/topdeck choice");
            });
        }
    }
}
