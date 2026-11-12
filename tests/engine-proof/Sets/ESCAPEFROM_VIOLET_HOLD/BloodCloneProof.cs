using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BloodCloneProof
{
    // data/rules-evidence/corpses/{blood-clone-full-board,fixed-spend-shortfall}.yaml (C/D; owner assumptions).
    internal static void Run() => DeathKnightC1Proof.Case("JAIL_451", Unpinned_BloodClone);
    private static void Unpinned_BloodClone(Game g, SabberStoneCore.Model.Entities.Controller p, string id)
    {
        foreach (int available in new[] { 0, 4, 5, 8 })
        foreach (string capacity in new[] { "normal", "full-board", "full-hand" })
        {
            var game = DeathKnightC1Proof.NewGame(g.Seed); var owner = game.Player1;
            CorpseRules.Gain(owner, available);
            var location = LocationProofs.Location(owner); var left = Summon(owner, "CS2_182");
            if (capacity == "full-board") while (!owner.BoardZone.IsFull) Summon(owner, "CS2_182");
            var source = Play(game, owner, id);
            var filter = OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5, maxCost: 5);
            var offers = owner.Choice.Choices.Select(n => game.IdEntityDic[n]).ToArray();
            var candidates = OfflineCardPools.Candidates(game, owner.HeroClass, filter, source.Card.AssetId);
            Core2026Proof.Check(candidates.Count >= 3 && offers.Length == 3 && offers.Select(o => o.Card.Id).Distinct().Count() == 3
                && offers.All(o => o.Card.Type == CardType.MINION && o.Card.Cost == 5 && o.Card.Id != id
                    && candidates.Any(c => c.Id == o.Card.Id)), "three distinct admitted DK/neutral 5-Cost minion offers");
            Core2026Proof.Check(CorpseRules.Available(owner) == available, "no spend before choice");
            if (capacity == "full-hand") PoolPilotProof.FillHand(owner);
            var replay = DeathKnightC1Proof.NewGame(g.Seed); CorpseRules.Gain(replay.Player1, available);
            LocationProofs.Location(replay.Player1); Summon(replay.Player1, "CS2_182");
            if (capacity == "full-board") while (!replay.Player1.BoardZone.IsFull) Summon(replay.Player1, "CS2_182");
            Play(replay, replay.Player1, id);
            Core2026Proof.Check(replay.Player1.Choice.Choices.Select(n => replay.IdEntityDic[n].Card.Id).SequenceEqual(offers.Select(o => o.Card.Id)), "same seed reproduces offers");
            int chosenId = offers[0].Id, slots = owner.BoardZone.SlotCount;
            PoolPilotProof.Resume(game, next => GiftedDiscoverProof.Choose(next, offers));
            var chosen = game.IdEntityDic[chosenId];
            Core2026Proof.Check(chosen.Zone.Type == (capacity == "full-hand" ? Zone.GRAVEYARD : Zone.HAND)
                && CorpseRules.Available(owner) == available - (available >= 5 ? 5 : 0), "Discover delivery and all-or-nothing five-Corpse payment");
            int expected = available >= 5 && capacity != "full-board" ? 1 : 0;
            Core2026Proof.Check(owner.BoardZone.SlotCount == slots + expected, "funded copy obeys physical capacity");
            if (expected != 0)
            {
                var copy = owner.BoardZone.Last();
                Core2026Proof.Check(copy.Card.Id == chosen.Card.Id && copy.Id != chosen.Id && copy.Health == copy.Card.Health
                    && copy[GameTag.DISPLAYED_CREATOR] == source.Id && copy.ZonePosition == slots,
                    "separate fresh chosen-definition copy appended");
            }
        }
    }
}
