using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class RiteOfAtrocityProof
{
    // data/rules-evidence/corpses/{rite-gift-offer-timing,fixed-spend-shortfall}.yaml (C/D; owner assumptions).
    internal static void Run() => DeathKnightC1Proof.Case("EDR_811", Unpinned_RiteOfAtrocity);
    private static void Unpinned_RiteOfAtrocity(Game g, SabberStoneCore.Model.Entities.Controller p, string id)
    {
        foreach (int available in new[] { 0, 1, 2, 4 })
        foreach (bool full in new[] { false, true })
        {
            var game = DeathKnightC1Proof.NewGame(g.Seed); var owner = game.Player1;
            CorpseRules.Gain(owner, available); var source = Play(game, owner, id);
            var filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.UNDEAD);
            var candidates = OfflineCardPools.Candidates(game, owner.HeroClass, filter, source.Card.AssetId);
            var offers = available >= 2 ? GiftedDiscoverProof.Offers(game, id, filter)
                : owner.Choice.Choices.Select(n => game.IdEntityDic[n]).ToArray();
            Core2026Proof.Check(candidates.Count >= 3 && offers.Length == 3 && offers.Select(o => o.Card.Id).Distinct().Count() == 3
                && offers.All(o => o.Card.Type == CardType.MINION && o.Card.IsRace(Race.UNDEAD) && o.Card.Id != id
                    && candidates.Any(c => c.Id == o.Card.Id)), "distinct admitted DK/neutral Undead offers");
            Core2026Proof.Check(CorpseRules.Available(owner) == available && offers.All(o => !DarkGifts.Has(o)), "offer-time gifts are previews, payment waits for selection");
            if (available < 2) Core2026Proof.Check(offers.All(o => o[GameTag.DARK_GIFT_ENTITY] == 0), "shortfall gives ordinary offers");
            var replay = DeathKnightC1Proof.NewGame(g.Seed); CorpseRules.Gain(replay.Player1, available);
            Play(replay, replay.Player1, id);
            Core2026Proof.Check(replay.Player1.Choice.Choices.Select(n => replay.IdEntityDic[n].Card.Id).SequenceEqual(offers.Select(o => o.Card.Id)), "same seed reproduces offers");
            // Fixed nonrelocating gift makes final zone and hand-capacity assertions unambiguous.
            if (available >= 2) GiftedDiscoverProof.Pair(game, offers[0], "EDR_100t5");
            if (full) PoolPilotProof.FillHand(owner);
            int chosenId = offers[0].Id;
            PoolPilotProof.Resume(game, next => Core2026Proof.Check(next.Process(ChooseTask.Pick(next.Player1, chosenId)), "resumed answer accepted"));
            var chosen = game.IdEntityDic[chosenId];
            Core2026Proof.Check(CorpseRules.Available(owner) == available - (available >= 2 ? 2 : 0)
                && DarkGifts.Has(chosen) == (available >= 2 && !full)
                && chosen.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND), "exact two paid once; only selected delivered card gets gift");
            Core2026Proof.Check(offers.Skip(1).All(o => !DarkGifts.Has(game.IdEntityDic[o.Id])) && owner.Choice == null
                && game.SpellCompletion == null, "unchosen never gifted; spell completes");
            PoolPilotProof.EmptyHand(owner); DeathKnightC1Proof.FullBoard(owner); Play(game, owner, id); DeathKnightC1Proof.Answer(game);
            Core2026Proof.Check(owner.BoardZone.SlotCount == 7, "Discover works on full mixed board");
        }
    }
}
