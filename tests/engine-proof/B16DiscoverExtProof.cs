using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16DiscoverExtProof
{
    [GroupProof("b16-discover-ext")]
    internal static void Run()
    {
        Core2026Proof.Check(Enum.TryParse<DiscoverEntitySource>("EnemyDeck", out var origin), "all-type enemy deck Discover source exists");
        var task = StateTaskList.Suspendable("proof/b16/enemy-deck", new DiscoverThenContinueTask(origin, DiscoverEntityAction.PutOnTop));
        for (int seed = 1; seed <= 8; seed++) {
            var g = DiscoverRealProof.New(seed); var p = g.Player1;
            var deck = DiscoverRealProof.Deck(p.Opponent, "CS2_029", "CS2_182", "CS2_106", "CS2_200", "CS2_029");
            var random = g.Random.Clone();
            var groups = deck.GroupBy(c => c.Card.Id).Select(c => c.ToArray()).ToList();
            var expected = new List<IPlayable[]>();
            while (expected.Count < 3) { int i = random.Next(groups.Count); expected.Add(groups[i]); groups.RemoveAt(i); }
            var bound = expected.Select(c => c.Length == 1 ? c[0].Id : c[random.Next(c.Length)].Id).ToArray();
            task.Process(g, p, Add(p, "CS2_200"), null);
            var offers = DiscoverRealProof.Offers(g);
            Core2026Proof.Check(offers.Select(DiscoverRealProof.Bound).SequenceEqual(bound), "seeded definition then representative order");
            Core2026Proof.Check(offers.All(c => c[Generic.PrivateDecisionOfferTag] == p.PlayerId), "private enemy deck proxies");
            DiscoverRealProof.Saved(g, next => {
                DiscoverRealProof.Answer(next, offers[0].Id);
                Core2026Proof.Check(next.Player2.DeckZone.Last().Id == bound[0], "selected enemy original on deck top");
                Core2026Proof.Check(next.Player2.DeckZone.Count == 5, "no copy/draw from enemy deck");
            });
        }
        Console.WriteLine("PASS b16 all-type enemy deck: seeded order, privacy markers, clone/snapshot");
    }
}
