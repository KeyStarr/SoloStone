using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class ShadowflameStalkerProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_924", (g, p, id) => {
        var filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DEMON);
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.WARLOCK }, filter, copy: true);
        foreach (bool sweet in new[] { false, true })
        {
            var game = GiftedDiscoverProof.New(g.Seed, CardClass.WARLOCK);
            var source = GiftedDiscoverProof.Open(game, id);
            var offers = GiftedDiscoverProof.Offers(game, id, filter);
            GiftedDiscoverProof.Pair(game, offers[0], sweet ? "EDR_100t8" : "EDR_100t5");
            PoolPilotProof.FillHand(game.Player1, 9);
            // dg11b-sweet-dreams-sequence and dg9-copy-retention: Sweet Dreams moves chosen
            // first; the later copy retains its gift in hand without a second relocation.
            PoolPilotProof.Resume(game, next => {
                GiftedDiscoverProof.Choose(next, offers);
                var chosen = next.IdEntityDic[offers[0].Id];
                Core2026Proof.Check(next.Player1.HandZone.Count == 10, "one free hand slot respected");
                var copy = next.IdEntityDic.Values.Single(x => x.Id != chosen.Id && x[GameTag.DISPLAYED_CREATOR] == source.Id && x.Card.Id == chosen.Card.Id);
                Core2026Proof.Check(copy.Zone.Type == (sweet ? Zone.HAND : Zone.GRAVEYARD), "copy fits only after Sweet Dreams frees slot");
                Core2026Proof.Check(DarkGifts.Has(copy) == sweet && chosen.Zone.Type == (sweet ? Zone.DECK : Zone.HAND), "copy gift and chosen destination");
            });
        }
    });
}
