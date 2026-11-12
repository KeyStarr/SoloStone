using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FollowRulesProof
{
    internal static Game New(int seed = 1, bool history = true)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 }) {
            foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
            p.BaseMana = 10;
        }
        return g;
    }

    internal static Enchantment Mark(IPlayable bearer, string id = "CAP_802e1")
    {
        var definition = Cards.FromId(id);
        Core2026Proof.Check(definition?.Implemented == true, "Follow grant definition missing: " + id);
        Generic.AddEnchantmentBlock(bearer.Game, definition, bearer.Controller.Hero, bearer);
        return bearer.AppliedEnchantments.Last();
    }

    [GroupProof("b15-follow")]
    internal static void Run()
    {
        Unpinned_RecursiveDeadline();
        Unpinned_RecipientSampling();
        Unpinned_ResidenceAndSilence();
        SecretLikeBearer();
        Unpinned_RewindGrant();
        Unpinned_AffordabilityBoundary();
        Console.WriteLine("PASS Follow facility: recursion/deadline, seeded recipients, lifecycle, Secret, clone/snapshot/Rewind, affordability");
    }

    private static void Unpinned_RecursiveDeadline()
    {
        foreach (bool history in new[] { true, false }) {
            var g = New(history: history); var p = g.Player1;
            var bearer = Add(p, "CS2_231"); Mark(bearer);
            Add(p, "CS2_231"); Add(p, "CS2_231"); Add(p, "CS2_231");
            foreach (var next in new[] { g, g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) }) {
                for (int i = 0; i < 3; i++) {
                    var held = next.Player1.HandZone.Single(c => c.AppliedEnchantments?.Any(e => e.Card.Id == "CAP_802e1") == true);
                    PlayEntity(next, next.Player1, held);
                    Core2026Proof.Check(next.Player1.BoardZone.Count(c => c.Card.Id == "CAP_802t") == i + 1,
                        "Unpinned_RecursiveDeadline: complete payload beyond two handoffs");
                }
                var last = next.Player1.HandZone.Single();
                End(next);
                Core2026Proof.Check(last.AppliedEnchantments?.Count == 0, "Unpinned_RecursiveDeadline: all recursive grants expire at original end");
                End(next); PlayEntity(next, next.Player1, last);
                Core2026Proof.Check(next.Player1.BoardZone.Count(c => c.Card.Id == "CAP_802t") == 3, "expired grant never fires");
            }
        }
    }

    private static void Unpinned_RecipientSampling()
    {
        var positions = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++) {
            var g = New(seed); var p = g.Player1;
            var first = Add(p, "CS2_231"); var second = Add(p, "CS2_231"); var third = Add(p, "CS2_231");
            var enemy = Add(p.Opponent, "CS2_231");
            var spell = Add(p, "CAP_802");
            var expected = g.CloneExact(history: true);
            int index = expected.Random.Next(3);
            Core2026Proof.Check(g.Process(PlayCardTask.Spell(p, spell)), "Follow play accepted");
            var candidates = new[] { first, second, third };
            Core2026Proof.Check(candidates.Select(c => c.AppliedEnchantments?.Count ?? 0).SequenceEqual(
                Enumerable.Range(0, 3).Select(i => i == index ? 1 : 0)), "Unpinned_RecipientSampling: exact seeded hand-order recipient");
            Core2026Proof.Check(g.Random.Next() == expected.Random.Next() && enemy.AppliedEnchantments?.Count is null or 0,
                "one RNG pick, opponent excluded");
            positions.Add(index);
        }
        Core2026Proof.Check(positions.SetEquals(new[] { 0, 1, 2 }), "seed vectors exercise every recipient position");
    }

    private static void Unpinned_ResidenceAndSilence()
    {
        var g = New(); var p = g.Player1;
        var bearer = Add(p, "CS2_231"); var actor = Mark(bearer);
        p.HandZone.Remove(bearer); Generic.ShuffleIntoDeck(p, p.Hero, bearer);
        Core2026Proof.Check(bearer.AppliedEnchantments.Count == 0 && actor.ActivatedTrigger == null,
            "leaving hand without playing removes grant");
        Generic.RemoveFromZone(p, bearer); Generic.AddHandPhase(p, bearer);
        PlayEntity(g, p, bearer);
        Core2026Proof.Check(!p.BoardZone.Any(c => c.Card.Id == "CAP_802t"), "returning bearer never resurrects old grant");
        var held = Add(p, "CS2_231"); Mark(held); ((Minion)held).Silence();
        PlayEntity(g, p, held);
        Core2026Proof.Check(!p.BoardZone.Any(c => c.Card.Id == "CAP_802t"), "Silence removes grant and subscription");
        var other = Add(p.Opponent, "CS2_231"); Mark(other);
        Play(g, p, "CS2_231");
        Core2026Proof.Check(!p.Opponent.BoardZone.Any(), "opponent-side grant never triggers on own play");
        End(g);
        Core2026Proof.Check(other.AppliedEnchantments.Count == 0, "grant on opposite side still expires at granting turn end");
    }

    private static void SecretLikeBearer()
    {
        var g = New(); var p = g.Player1;
        var secret = Add(p, "EX1_287"); Mark(secret); var next = Add(p, "CS2_231");
        PlayEntity(g, p, secret);
        Core2026Proof.Check(p.SecretZone.Contains(secret) && p.BoardZone.Single().Card.Id == "CAP_802t"
            && next.AppliedEnchantments.Count == 1, "36.4.1: Secret-like bearer summons and regrants");
    }

    private static void Unpinned_RewindGrant()
    {
        var g = New(); var p = g.Player1;
        var knives = Add(p, "TIME_001"); Mark(knives); Add(p, "CS2_231");
        PlayEntity(g, p, knives);
        Core2026Proof.Check(g.Rewind != null && p.BoardZone.Count(c => c.Card.Id == "CAP_802t") == 1,
            "Rewind offer waits for Follow payload");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var transaction = next.Rewind;
            Core2026Proof.Check(next.TryAnswer(new DecisionAnswer(transaction.OfferChoiceId, transaction.ControllerId,
                new[] { transaction.RewindEntityId }), out var error), "Rewind answer: " + error);
            Core2026Proof.Check(next.Player1.BoardZone.Count(c => c.Card.Id == "CAP_802t") == 1
                && next.Player1.HandZone.Single().AppliedEnchantments.Count == 1,
                "Unpinned_RewindGrant: baseline restores grant, retry consumes once and keeps one recursive successor");
        }
    }

    private static void Unpinned_AffordabilityBoundary()
    {
        var g = New(); var p = g.Player1;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        var discounted = Add(p, "CS2_200"); discounted.Cost = 0;
        var overBudget = Add(p, "CS2_231"); overBudget.Cost = 1;
        var source = Add(p, "CAP_802");
        p.BaseMana = 2;
        Core2026Proof.Check(g.Process(PlayCardTask.Spell(p, source)), "zero remaining mana grant");
        Core2026Proof.Check(p.RemainingMana == 0 && discounted.AppliedEnchantments.Single().Card.Id == "CAP_802e1"
            && overBudget.AppliedEnchantments?.Count is null or 0,
            "Unpinned_AffordabilityBoundary: discounted zero-cost minion eligible on full board after payment");
        Core2026Proof.Check(!g.Process(PlayCardTask.Minion(p, discounted)) && discounted.Zone == p.HandZone,
            "recipient selection does not bypass later board legality");
        p.HandZone.Remove(discounted); p.SetasideZone.Add(discounted);
        source = Add(p, "CAP_802"); p.UsedMana = 0;
        var rng = g.Random.Clone();
        Core2026Proof.Check(g.Process(PlayCardTask.Spell(p, source)) && g.Random.Next() == rng.Next()
            && p.Choice == null && overBudget.AppliedEnchantments?.Count is null or 0,
            "empty eligible set consumes no random value and creates no choice");
    }
}
