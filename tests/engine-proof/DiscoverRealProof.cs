using SabberStoneCore.Model;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class DiscoverRealProof
{
    private static readonly SuspendableTask Draw = StateTaskList.Suspendable("proof/discover-real/draw/v1",
        new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw));
    private static readonly SuspendableTask ControllerChoice = StateTaskList.Suspendable("proof/discover-real/controller-source/v1",
        new DiscoverThenContinueTask("CS2_182"));
    internal static Game New(long seed = 1) => GiftedDiscoverProof.New(seed, CardClass.HUNTER);
    internal static IPlayable[] Deck(Controller owner, params string[] ids)
    {
        foreach (var old in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(old));
        return ids.Select(id => Entity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone)).ToArray();
    }
    internal static IPlayable[] Offers(Game g, Controller owner = null) =>
        (owner ?? g.CurrentPlayer).Choice?.Choices.Select(id => g.IdEntityDic[id]).ToArray() ?? Array.Empty<IPlayable>();
    internal static int Bound(IPlayable offer) => offer[DiscoverRealRules.BoundEntity];
    internal static void Answer(Game g, int selected, Controller owner = null)
    {
        var decision = g.GetPendingDecisions().Single(d => d.ControllerId == (owner ?? g.CurrentPlayer).Id);
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out var error), "real choice answer: " + error);
    }
    internal static void Saved(Game g, Action<Game> action) => PoolPilotProof.Resume(g, action);
    internal static void Assert(bool value, string message) => Core2026Proof.Check(value, message);

    internal static void RunRegressions(string only = null)
    {
        if (only == null || only == "reload-shatter") Unpinned_ReloadDiscountBeforeShatter();
        if (only == null || only == "geddon-shatter") Unpinned_GeddonDiscountBeforeShatter();
        if (only == null || only == "reload-aura") Unpinned_ReloadSamplesHandAuras();
    }

    // cost-sampling/excess-reduction-budget: then-current hand cost; carried budget survives snapshots.
    private static void Unpinned_ReloadDiscountBeforeShatter()
    {
        var g = PinnedPoolFixture.Use(New(10)); var p = g.Player1;
        Add(p, "CS2_182"); // Keep complementary halves apart so both inherited discounts are observable.
        var source = Play(g, p, "BE_555");
        var offered = Offers(g).Single(o => o.Card.Id == "CATA_820");
        Saved(g, next => {
            Answer(next, offered.Id);
            var halves = next.Player1.HandZone.Where(c => c.Card.Shatter?.ParentId == "CATA_820").ToArray();
            Assert(halves.Length == 2 && halves.All(c => c.Cost == 0), "Reload discounts both Supply Run halves before retirement");
            Assert(next.Player1.Choice != null && next.IdEntityDic[source.Id][DiscoverRealRules.Budget] == 4,
                "Reload carries four remaining reduction through Shatter");
        });
        // Resume the next saved choice too: the remaining four is neither lost nor reset to eight.
        var second = Offers(g).First(); second.Cost = 6;
        Saved(g, next => {
            Answer(next, second.Id);
            Assert(next.Player1.Choice == null && next.IdEntityDic[second.Id].Cost == 2
                && next.IdEntityDic[source.Id][DiscoverRealRules.Budget] == 0, "resumed Reload spends only remaining four");
        });
        Console.WriteLine("PASS Reload discount and budget survive Shatter, clone/snapshot");
    }

    private static void Unpinned_GeddonDiscountBeforeShatter()
    {
        var g = New(); var p = g.Player1;
        Add(p, "CS2_182"); Play(g, p, "CATA_591"); Deck(p, "CATA_820");
        End(g); End(g);
        int offer = Offers(g).Single().Id;
        Saved(g, next => {
            Answer(next, offer);
            var halves = next.Player1.HandZone.Where(c => c.Card.Shatter?.ParentId == "CATA_820").ToArray();
            Assert(halves.Length == 2 && halves.All(c => c.Cost == 1), "Geddon discounts both Supply Run halves before retirement");
        });
        Console.WriteLine("PASS Geddon discount survives Shatter, clone/snapshot");
    }

    private static void Unpinned_ReloadSamplesHandAuras()
    {
        var g = PinnedPoolFixture.Use(New()); var p = g.Player1;
        Summon(p, "EX1_608");
        var source = Play(g, p, "BE_555");
        var offered = Offers(g).Single(o => o.Card.Id == "FIR_909");
        Saved(g, next => {
            Answer(next, offered.Id);
            Assert(next.Player1.Choice != null && next.IdEntityDic[offered.Id].Cost == 0
                && next.IdEntityDic[source.Id][DiscoverRealRules.Budget] == 7,
                "Reload samples Apprentice hand aura before debit: two minus one leaves seven");
        });
        Console.WriteLine("PASS Reload samples current hand cost with aura, clone/snapshot");
    }

    internal static void Run()
    {
        RunRegressions();
        for (int seed = 1; seed <= 3; seed++)
        for (int size = 0; size <= 4; size++)
        {
            var g = New(seed); var p = g.Player1;
            var originals = Deck(p, Enumerable.Repeat("CS2_182", size).ToArray());
            var source = Add(p, "CS2_200");
            Draw.Process(g, p, source, null);
            var offers = Offers(g);
            Assert(offers.Length == (size == 0 ? 0 : 1), "deck definitions deduplicated at all short sizes");
            if (size == 0) continue;
            int bound = Bound(offers[0]);
            Assert(originals.Any(p => p.Id == bound) && offers[0].Id != bound && offers[0][Generic.PrivateDecisionOfferTag] == 1,
                "private offer binds a real original without revealing/moving it");
            Saved(g, next => {
                Answer(next, offers[0].Id);
                Assert(next.IdEntityDic[bound].Zone == next.Player1.HandZone && next.Player1.DeckZone.Count == size - 1,
                    "draw moves exact entity rather than creating a card");
                Assert(next.IdEntityDic[offers[0].Id].Zone == next.Player1.SetasideZone, "display proxy is never delivered");
            });
        }
        var stale = New(); var owner = stale.Player1;
        Deck(owner, "CS2_182", "CS2_200", "CS2_231", "CS2_029");
        Draw.Process(stale, owner, Add(owner, "CS2_200"), null);
        var offer = Offers(stale)[0]; var real = stale.IdEntityDic[Bound(offer)];
        owner.GraveyardZone.Add(owner.DeckZone.Remove(real));
        var before = stale.SaveSnapshot(); var pending = stale.GetPendingDecisions().Single();
        Assert(!stale.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { offer.Id }), out _)
            && before.SequenceEqual(stale.SaveSnapshot()), "stale binding rejects without state/history/RNG changes");
        var legacy = New();
        ControllerChoice.Process(legacy, legacy.Player1, legacy.Player1, null);
        int legacyOffer = Offers(legacy).Single().Id;
        Saved(legacy, next => Answer(next, legacyOffer));
        Console.WriteLine("PASS real entity selection: short/duplicate pools, entity movement, stale answer, clone/snapshot");
    }
}
