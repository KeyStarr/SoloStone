using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16AutoplayPrisonProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_887", (g, c, id) => { Unpinned_Prison(g, c, id);
            var fresh = LocationProofs.Game(); Unpinned_PrisonStateAndCopies(fresh, fresh.Player1, id); });
        StandardSetsProof.Case("JAIL_500", Unpinned_Slice);

        SliceSeededOrder();
        Pinned_ZuramatRandomWithoutReplacement();
    }
    private static void Pinned_ZuramatRandomWithoutReplacement()
    {
        foreach (var sample in new[] { (Seed: 1, Order: new[] { 0, 2, 1 }),
            (Seed: 8, Order: new[] { 0, 1, 2 }), (Seed: 19, Order: new[] { 2, 1, 0 }) })
        foreach (bool reverse in new[] { false, true })
        {
            var game = LocationProofs.Game(); var p = game.Player1;
            var prison = LocationProofs.Play(p, "JAIL_887");
            var cards = new[] { "CS2_182", "CS2_120", "CS2_179" };
            if (reverse) Array.Reverse(cards);
            foreach (string id in cards)
            {
                var discard = Add(p, id);
                B16AutoplayProof.Ready(prison); LocationProofs.Use(prison);
                B16AutoplayProof.Answer(game, discard.Id);
            }
            var zuramat = p.BoardZone.Single(m => m.Card.Id == "JAIL_887t2");
            var entries = PayloadRules.Entries(zuramat);
            Core2026Proof.Check(entries.Select(e => e.Card.Id).SequenceEqual(cards), "Prison transfers all three discards in order");
            game.SetRandomSeed(sample.Seed);
            foreach (var next in B16AutoplayProof.Futures(game))
            {
                var host = next.IdEntityDic[zuramat.Id]; var owner = next.Player1;
                for (int turn = 0; turn < 4; turn++)
                {
                    long draws = next.Random.DrawCount;
                    End(next);
                    var consumed = sample.Order.Take(Math.Min(turn + 1, 3)).ToArray();
                    Core2026Proof.Check(owner.BoardZone.Skip(4).Select(m => m.Card.Id)
                        .SequenceEqual(consumed.Select(i => cards[i])),
                        "Zuramat seeded replay order and exhaustion, seed " + sample.Seed + ", reverse " + reverse + ", turn " + turn);
                    var remaining = entries.Where((e, i) => !consumed.Contains(i)).Select(e => e.Id).ToArray();
                    Core2026Proof.Check(PayloadRules.Entries(host).Select(e => e.Id).SequenceEqual(remaining)
                        && next.Random.DrawCount == draws + (turn < 3 ? 1 : 0),
                        "Zuramat removes precisely chosen entry, one RNG draw per remaining-card attempt, none after exhaustion");
                    B16AutoplayProof.AssertSaved(next);
                    var rng = next.Random.CaptureState();
                    End(next);
                    Core2026Proof.Check(PayloadRules.Entries(host).Select(e => e.Id).SequenceEqual(remaining)
                        && owner.BoardZone.SlotCount == 4 + consumed.Length && next.Random.CaptureState().Equals(rng),
                        "opponent end neither replays nor consumes ledger/RNG");
                }
            }
        }
    }
    private static void Unpinned_Prison(Game game, Controller p, string id)
    {
        var prison = LocationProofs.Play(p, id);
        var card = Add(p, "CS2_189");
        prison[GameTag.DAMAGE] = 2;
        LocationProofs.Use(prison);
        Core2026Proof.Check(prison.Zone == p.GraveyardZone && p.Choice != null && !p.BoardZone.Any(), "final Prison frees slot but defers Deathrattle");
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            B16AutoplayProof.Answer(next, card.Id);
            var owner = next.Player1;
            Core2026Proof.Check(owner.BoardZone.Any(m => m.Card.Id == "JAIL_887t3"), "Prison summons 5/5 after actual discard");
            var zuramat = owner.BoardZone.Single(m => m.Card.Id == "JAIL_887t2");
            Core2026Proof.Check(PayloadRules.Entries(zuramat).Single().Card.Id == card.Card.Id, "Prison transfers its own ledger");
            End(next);
            Core2026Proof.Check(owner.BoardZone.Any(m => m.Card.Id == "CS2_189") && owner.Opponent.Hero.Damage == 0
                && PayloadRules.Entries(zuramat).Length == 0, "Zuramat consumes one identity without Battlecry");
        }
    }
    private static void Unpinned_PrisonStateAndCopies(Game game, Controller p, string id)
    {
        var first = LocationProofs.Play(p, id); var second = LocationProofs.Play(p, id);
        var archer = (Minion)Add(p, "CS2_189"); archer.AttackDamage = 9; archer.Cost = 0;
        archer[GameTag.TAG_SCRIPT_DATA_NUM_1] = 77;
        LocationProofs.Use(first); B16AutoplayProof.Answer(game, archer.Id);
        var yeti = Add(p, "CS2_182");
        LocationProofs.Use(second); B16AutoplayProof.Answer(game, yeti.Id);
        Core2026Proof.Check(PayloadRules.Entries(first).Single().Card.Id == "CS2_189" &&
            PayloadRules.Entries(second).Single().Card.Id == "CS2_182", "two Prison ledgers stay disjoint");
        var saved = (Minion)PayloadRules.Entries(first).Single();
        Core2026Proof.Check(saved.AttackDamage == saved.Card.ATK && saved.Cost == saved.Card.Cost && saved[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
            "identity ledger drops hand stats, discount and instance counters");
        first.Destroy(); second.Destroy(); game.DeathProcessingAndAuraUpdate();
        var original = p.BoardZone.Single(m => m.Card.Id == "JAIL_887t2" && PayloadRules.Entries(m).Single().Card.Id == "CS2_189");
        var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY);
        var fresh = Summon(p, "JAIL_887t2");
        Core2026Proof.Check(PayloadRules.Entries(fresh).Length == 0 && PayloadRules.Entries(copy).Single().Id != PayloadRules.Entries(original).Single().Id,
            "base Zuramat has no ledger, exact copy has independent entries");
        original.Silence();
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        game.DeathProcessingAndAuraUpdate();
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            var owner = next.Player1; End(next);
            Core2026Proof.Check(owner.BoardZone.SlotCount == 7 && PayloadRules.Entries(next.IdEntityDic[copy.Id]).Length == 0
                && PayloadRules.Entries(next.IdEntityDic[original.Id]).Length == 0,
                "silenced source clears its ledger; full-board copied attempt consumed without replacement");
        }
        var rewind = HeroDivineShieldProof.NewGame(true);
        var host = Summon(rewind.Player1, "JAIL_887t2");
        var entry = PayloadRules.StoreCard(host, 126497, Cards.FromId("CS2_189"));
        Play(rewind, rewind.Player1, "TIME_001");
        foreach (var next in B16AutoplayProof.Futures(rewind))
        {
            PayloadRules.Remove(next.IdEntityDic[entry.Id]);
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(PayloadRules.Entries(next.IdEntityDic[host.Id]).Single().Card.Id == "CS2_189", "Rewind restores remaining Zuramat entry");
            RewindTestHelpers.KeepIfOffered(next);
        }
    }
    private static void SliceSeededOrder()
    {
        foreach (int seed in new[] { 1, 8, 19 })
        {
            var game = LocationProofs.Game(seed); var p = game.Player1;
            var expected = new[] { "CS2_182", "CS2_120", "CS2_179" };
            foreach (string card in expected) Play(game, p, card);
            var rng = game.Random.Clone();
            for (int i = expected.Length - 1; i > 0; i--)
            { int j = rng.Next(i + 1); (expected[i], expected[j]) = (expected[j], expected[i]); }
            foreach (var next in B16AutoplayProof.Futures(game))
            {
                Play(next, next.Player1, "JAIL_500");
                Core2026Proof.Check(next.Player1.BoardZone.Skip(3).Select(m => m.Card.Id).SequenceEqual(expected), "Slice seeded replay order, seed " + seed);
                Core2026Proof.Check(next.Random.CaptureState().Equals(rng.CaptureState()), "Slice consumes shuffle RNG exactly once");
            }
        }
    }
    private static void Unpinned_Slice(Game game, Controller p, string id)
    {
        Play(game, p, "CS2_189", p.Opponent.Hero);
        Play(game, p, "CS2_106");
        int turn = game.Turn;
        Play(game, p, id);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_189") == 2 && p.Opponent.Hero.Damage == 1,
            "Slice summons replayed minion without Battlecry");
        Core2026Proof.Check(game.Turn == turn + 1 && game.CurrentPlayer == p.Opponent && p.Hero.Weapon.Card.Id == "CS2_106", "Slice finishes mixed replay before ending turn once");
    }
}
