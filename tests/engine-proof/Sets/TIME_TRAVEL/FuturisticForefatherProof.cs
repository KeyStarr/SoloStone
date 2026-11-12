using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class FuturisticForefatherProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_041", Unpinned_SavedCorrectnessAndFallback);
    private static void Unpinned_SavedCorrectnessAndFallback(Game original, Controller ignored, string id)
    {
        foreach (bool empty in new[] { false, true })
        foreach (bool fallback in new[] { false, true })
        foreach (bool correct in new[] { true, false })
        {
            var g = New(original.Seed); var p = g.Player1;
            if (!empty) Add(p.Opponent, "CS2_029");
            var deck = fallback ? Deck(p.Opponent) : Deck(p.Opponent, "CS2_029", "CS2_182", "CS2_200");
            var source = (Minion)Play(g, p, id); var offers = Offers(g);
            Assert(offers.Length == 3 && offers.Select(o => o.Card.Name).Distinct().Count() == 3, "three distinct-name offers, current-deck decoys and fallback");
            Assert(offers.Count(o => o[DiscoverRealRules.CorrectAnswer] == 1) == (empty ? 0 : 1), "only saved hand-derived answer can be correct");
            var chosen = offers.First(o => o[DiscoverRealRules.CorrectAnswer] == (!empty && correct ? 1 : 0));
            // Membership after creation cannot change the saved designated winner.
            foreach (var held in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(held));
            Saved(g, next => {
                Answer(next, chosen.Id); var minion = (Minion)next.IdEntityDic[source.Id];
                Assert(minion.HasTaunt && minion.Health == (!empty && correct ? 8 : 4), "saved correctness grants exactly +4 Health");
                Assert(next.Player2.DeckZone.Select(c => c.Id).SequenceEqual(deck.Select(c => c.Id)), "guess consumes no originals");
                minion.Silence(); Assert(minion.Health == 4, "guess buff removed by Silence");
            });
        }
    }
}
