using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static B15DrawReplacementProof;

internal static class B15DrawReplacementTimeProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("TIME_032", Unpinned_Chronogor);
        StandardSetsProof.Case("TIME_617", Chronochiller);
    }
    private static void Unpinned_Chronogor(Game g, Controller p, string id)
    {
        EmptyDeck(p);
        foreach (string card in new[] { "CS2_231", "CS2_029", "CS2_182", "CS2_200" })
            Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.IsEmpty && p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 2
            && p.HandZone.All(c => c.Cost >= 4) && p.Opponent.HandZone.All(c => c.Cost <= 4),
            "Chronogor owner highest then enemy lowest via cross-controller draw");
        for (int size = 0; size <= 3; size++)
        {
            var next = NewGame(size + 1); var owner = next.Player1; EmptyDeck(owner);
            for (int i = 0; i < size; i++) Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            Play(next, owner, id);
            Core2026Proof.Check(owner.HandZone.Count == Math.Min(2, size)
                && owner.Opponent.HandZone.Count == (size == 3 ? 1 : 0)
                && owner.Hero.Damage == 0 && owner.Opponent.Hero.Damage == 0,
                "Unpinned_Chronogor short deck owner-first allocation without missing-selection fatigue: " + size);
        }
        var lethal = NewGame(); var caster = lethal.Player1; EmptyDeck(caster);
        caster.Hero.Damage = 27;
        for (int i = 0; i < 2; i++) Entity.FromCard(caster, Cards.FromId("JAIL_398"), zone: caster.DeckZone);
        for (int i = 0; i < 2; i++) Entity.FromCard(caster, Cards.FromId("CS2_231"), zone: caster.DeckZone);
        var source = Add(caster, id);
        while (!caster.HandZone.IsFull) Add(caster, "CS2_182");
        Core2026Proof.Check(lethal.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(caster, source))
            && lethal.State == State.COMPLETE && caster.Opponent.HandZone.IsEmpty && caster.DeckZone.Count == 2,
            "Chronogor stops remaining cross-draws after lethal burned Deathrattle");
        for (int seed = 1; seed <= 4; seed++)
        {
            var next = NewGame(seed); var owner = next.Player1; EmptyDeck(owner);
            var high = Enumerable.Range(0, 4).Select(_ => Entity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.DeckZone)).ToList();
            var low = Enumerable.Range(0, 3).Select(_ => Entity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.DeckZone)).ToList();
            // Independent RNG cursor and hand-constructed tie buckets catch first-entity/tie-weighting mutations.
            next.SetRandomSeed(seed);
            var rng = new SabberStoneCore.Model.Util.DeepCloneableRandom(seed);
            var expectedHigh = new List<int>(); var expectedLow = new List<int>();
            for (int i = 0; i < 2; i++) { int pick = rng.Next(high.Count); expectedHigh.Add(high[pick].Id); high.RemoveAt(pick); }
            for (int i = 0; i < 2; i++) { int pick = rng.Next(low.Count); expectedLow.Add(low[pick].Id); low.RemoveAt(pick); }
            foreach (var variant in new[] { next, next.CloneExact(history: true), Game.LoadSnapshot(next.SaveSnapshot()) })
            {
                Play(variant, variant.Player1, id);
                Core2026Proof.Check(variant.Player1.HandZone.Select(c => c.Id).SequenceEqual(expectedHigh)
                    && variant.Player2.HandZone.Select(c => c.Id).SequenceEqual(expectedLow),
                    "Unpinned_Chronogor seeded ordered physical ties survive clone/load");
            }
        }
    }
    private static void Chronochiller(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g, p, id);
        int before = p.DeckZone.Count;
        g.MainDraw();
        Core2026Proof.Check(p.DeckZone.Count == before && p.HandZone.IsEmpty, "Chronochiller suppresses normal draw");
        Generic.Draw(p);
        Core2026Proof.Check(p.DeckZone.Count == before - 1, "explicit draw unaffected");
        int enemyDeck = p.Opponent.DeckZone.Count;
        End(g);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == enemyDeck - 1, "opponent normal draw unaffected");
        End(g);
        Core2026Proof.Check(p.DeckZone.Count == before - 1, "suppression persists across turns");
        EmptyDeck(p);
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            next.MainDraw();
            Core2026Proof.Check(next.Player1.Hero.Damage == 0, "active suppression prevents fatigue after clone/load");
            ((Minion)next.IdEntityDic[body.Id]).Silence(); next.MainDraw();
            Core2026Proof.Check(next.Player1.Hero.Damage == 1, "Silence resumes empty-deck fatigue");
        }
        p.SetasideZone.Add(p.BoardZone.Remove(body)); g.MainDraw();
        Core2026Proof.Check(p.Hero.Damage == 1, "leave-play resumes normal fatigue");
    }
}
