using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static class JumpscareProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_882", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        var owner = replay.Player1;
        int deck = owner.DeckZone.Count;
        // Tess must finish the replayed Discover, not consume its choice and strand the recipe.
        owner.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Core2026Proof.Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay completes Discover synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess replay completes cast once");
            Core2026Proof.Check(next.Player1.HandZone.Concat(next.Player1.DeckZone).Count(DarkGifts.Has) == 1
                && next.Player1.HandZone.Count + next.Player1.DeckZone.Count == deck + 3,
                "Tess Jumpscare grants one gifted Demon and shuffles unchosen offers");
            Core2026Proof.End(next);
        });

        var filter = OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5, race: Race.DEMON);
        // D9 / dg10-jumpscare-shuffled-offers: shuffle the unchosen offers without gifts.
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.DEMONHUNTER }, filter, shuffle: true);
        foreach (int free in new[] { 0, 1 })
        {
            var game = GiftedDiscoverProof.New(g.Seed, CardClass.DEMONHUNTER);
            GiftedDiscoverProof.Open(game, id);
            var offers = GiftedDiscoverProof.Offers(game, id, filter);
            GiftedDiscoverProof.Pair(game, offers[0], "EDR_100t5");
            while (!game.Player1.DeckZone.IsFull) Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.DeckZone);
            for (int i = 0; i < free; i++) game.Player1.SetasideZone.Add(game.Player1.DeckZone.Remove(game.Player1.DeckZone.TopCard));
            int before = game.Player1.DeckZone.Count;
            PoolPilotProof.Resume(game, next => {
                GiftedDiscoverProof.Choose(next, offers);
                Core2026Proof.Check(next.Player1.DeckZone.Count == before + free, "full/one-free deck slot respected");
                Core2026Proof.Check(offers.Skip(1).Count(x => next.IdEntityDic[x.Id].Zone == next.Player1.DeckZone) == free,
                    "shuffle only offers that fit");
            });
        }
    });
}
