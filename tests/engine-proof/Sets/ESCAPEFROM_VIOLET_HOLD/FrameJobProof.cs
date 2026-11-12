using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class FrameJobProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_403", Unpinned_EnemyTopdeck);
    private static void Unpinned_EnemyTopdeck(Game g, Controller p, string id)
    {
        var deck = Deck(p.Opponent, "CS2_029", "CS2_182", "CS2_182", "CS2_200");
        foreach (int i in Enumerable.Range(0, 3)) Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        var offers = Offers(g); int bound = Bound(offers[0]);
        Assert(p.Opponent.BoardZone.Count == 1 && offers.Length == 2 && offers.All(o => o is Minion),
            "destroys two different enemies before minion-only deduplicated enemy-deck choice");
        Saved(g, next => {
            Answer(next, offers[0].Id);
            Assert(next.Player2.DeckZone.Count == deck.Length && next.Player2.DeckZone.TopCard.Id == bound
                && next.Player1.HandZone.Count == 0, "moves bound enemy entity to top without draw/copy");
        });
        Deck(p.Opponent, "CS2_029"); Play(g, p, id);
        Assert(p.Choice == null && p.Opponent.BoardZone.Count == 0, "short board destroyed, no eligible minion means no prompt");
    }
}
