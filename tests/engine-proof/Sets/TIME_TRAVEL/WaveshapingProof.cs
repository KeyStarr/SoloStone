using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class WaveshapingProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_701", Unpinned_RejectedBottomGroup);
    private static void Unpinned_RejectedBottomGroup(Game g, Controller p, string id)
    {
        var deck = Deck(p, "CS2_182", "CS2_200", "CS2_231", "CS2_029", "CS2_172");
        Play(g, p, id); var offers = Offers(g); int chosen = Bound(offers[0]);
        int[] rejected = offers.Skip(1).Select(Bound).ToArray();
        int[] untouched = deck.Select(c => c.Id).Except(offers.Select(Bound)).ToArray();
        Saved(g, next => {
            Answer(next, offers[0].Id); var owner = next.Player1;
            Assert(next.IdEntityDic[chosen].Zone == owner.HandZone && owner.DeckZone.Count == 4, "selected real deck card drawn");
            Assert(owner.DeckZone.Take(2).Select(c => c.Id).Order().SequenceEqual(rejected.Order()), "only rejected offers form bottom group");
            Assert(owner.DeckZone.Skip(2).Select(c => c.Id).SequenceEqual(untouched), "unoffered relative order preserved");
        });
        Deck(p); Play(g, p, id); Assert(p.Choice == null, "empty deck no choice/fatigue");
        Deck(p, "CS2_182", "CS2_182"); Play(g, p, id); var single = Offers(g).Single();
        int remaining = p.DeckZone.Single(c => c.Id != Bound(single)).Id;
        Answer(g, single.Id);
        Assert(p.DeckZone.Single().Id == remaining, "unoffered duplicate not bottomed or destroyed");
    }
}
