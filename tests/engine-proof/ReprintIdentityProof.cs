using SabberStoneCore.Config;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Identity;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Actions;

// #225: a card and its reprints are one card for every rule that compares cards. The retail rule is not pinned by a Blizzard
// source or trace, so every proof here is labelled Unpinned_* and says what the engine decides, not what retail is known to do.
internal static class ReprintIdentityProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "ReprintIdentity: " + message);

    internal static void Run()
    {
        Unpinned_DataCheck();
        var failures = new List<string>();
        foreach (var (name, part) in new (string, Action)[] {
            ("SecretUniquenessBothWays", Unpinned_SecretUniquenessBothWays),
            ("AutomaticCastSkipsOtherPrinting", Unpinned_AutomaticCastSkipsOtherPrinting),
            ("RandomSecretPickSkipsOtherPrinting", Unpinned_RandomSecretPickSkipsOtherPrinting),
            ("HighlanderAcrossPrintings", Unpinned_HighlanderAcrossPrintings),
            ("DiscoverNeverOffersTwoPrintings", Unpinned_DiscoverNeverOffersTwoPrintings),
            ("SourceExclusion", Unpinned_SourceExclusion),
            ("PlayedHistoryAcrossPrintings", Unpinned_PlayedHistoryAcrossPrintings),
            ("LiteralNamesListEveryPrinting", Unpinned_LiteralNamesListEveryPrinting),
            ("PlayedMinionHistoryAcrossPrintings", Unpinned_PlayedMinionHistoryAcrossPrintings),
            ("MinionPoolSamplingRedraw", Unpinned_MinionPoolSamplingRedraw) })
            try { part(); Console.WriteLine("PASS Unpinned_ReprintIdentity " + name); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); }
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
    }

    private static Game Fixture(int seed = 7)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true,
            Logging = false, FormatType = FormatType.FT_WILD, PoolFormat = GenerationFormat.Wild,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
            p.BaseMana = 10;
        }
        return game;
    }

    private static Spell ToDeck(Controller p, string id) => (Spell)Entity.FromCard(p, Cards.FromId(id), null, p.DeckZone);

    private static bool Playable(Controller p, IPlayable card) =>
        card.IsPlayableByPlayer && p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);

    // Unpinned_: a Secret cannot be played while its other printing is active, in either order, for each Core Secret pair.
    private static void Unpinned_SecretUniquenessBothWays()
    {
        foreach (var (core, classic) in new[] { ("CORE_EX1_289", "EX1_289"), ("CORE_EX1_287", "EX1_287"), ("CORE_EX1_610", "EX1_610"), ("CORE_EX1_611", "EX1_611") })
            foreach (var (first, second) in new[] { (core, classic), (classic, core), (classic, classic) })
            {
                var g = Fixture(); var p = g.Player1;
                Core2026Proof.Play(g, p, first);
                Verify(p.SecretZone.Count == 1, first + " is active");
                var other = Core2026Proof.Add(p, second);
                p.BaseMana = 10; p.UsedMana = 0;
                Verify(!Playable(p, other), second + " is not playable while " + first + " is active");
            }
    }

    // Unpinned_: an automatic cast of a Secret from the deck does nothing while its other printing is active (CastSpell and Mad Scientist's Deathrattle).
    private static void Unpinned_AutomaticCastSkipsOtherPrinting()
    {
        foreach (var (active, deck) in new[] { ("EX1_289", "CORE_EX1_289"), ("CORE_EX1_289", "EX1_289") })
        {
            var g = Fixture(); var p = g.Player1;
            Core2026Proof.Play(g, p, active);
            var spell = ToDeck(p, deck);
            p.DeckZone.Remove(spell);
            Generic.CastSpell(p, g, spell, null, 0);
            Verify(p.SecretZone.Count == 1, "CastSpell did not cast " + deck + " beside active " + active + ", Secrets=" + p.SecretZone.Count);

            var h = Fixture(); var q = h.Player1;
            Core2026Proof.Play(h, q, active);
            ToDeck(q, deck);
            var scientist = Core2026Proof.Summon(q, "FP1_004");
            Core2026Proof.Kill(h, scientist);
            Verify(q.SecretZone.Count == 1, "Mad Scientist did not put " + deck + " beside active " + active + ", Secrets=" + q.SecretZone.Count);
        }
    }

    // Unpinned_: random Secret picks (Arcane Keysmith, Kel'Thuzad-style deck casts, draw-one-of-each) skip a Secret whose other printing is active.
    private static void Unpinned_RandomSecretPickSkipsOtherPrinting()
    {
        // Arcane Keysmith offers three Mage Secrets, never the active one in either printing and never two printings of one Secret.
        int offered = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var g = Fixture(seed); var p = g.Player1;
            Core2026Proof.Play(g, p, "CORE_EX1_289");
            Core2026Proof.PlayEntity(g, p, Core2026Proof.Add(p, "GIL_116"));
            Verify(p.Choice != null, "Arcane Keysmith offers a choice (seed " + seed + ")");
            var cards = p.Choice.Choices.Select(id => g.IdEntityDic[id].Card).ToArray();
            offered += cards.Length;
            Verify(!cards.Any(c => c.IsSameCardAs(Cards.FromId("EX1_289"))), "Arcane Keysmith never offers Ice Barrier beside active Core Ice Barrier (seed " + seed + "): " + string.Join(",", cards.Select(c => c.Id)));
            Verify(cards.Select(c => c.Identity).Distinct().Count() == cards.Length, "Arcane Keysmith never offers two printings of one Secret (seed " + seed + "): " + string.Join(",", cards.Select(c => c.Id)));
        }
        Verify(offered > 0, "Arcane Keysmith offered Secrets");

        // Kel'Thuzad-style cast from deck and draw-one-of-each: one of the two printings only.
        var h = Fixture(); var q = h.Player1;
        Core2026Proof.Play(h, q, "EX1_289");
        ToDeck(q, "CORE_EX1_289"); ToDeck(q, "EX1_287");
        Core2026Proof.PlayEntity(h, q, Core2026Proof.Add(q, "ICC_086"));
        Verify(q.SecretZone.Count == 2 && q.SecretZone.Count(s => s.Card.IsSameCardAs(Cards.FromId("EX1_289"))) == 1,
            "Icecrown cast from deck adds Counterspell but not a second Ice Barrier, Secrets=" + string.Join(",", q.SecretZone.Select(s => s.Card.Id)));

        var d = Fixture(); var r = d.Player1;
        ToDeck(r, "CORE_EX1_289"); ToDeck(r, "EX1_289"); ToDeck(r, "EX1_287");
        Core2026Proof.PlayEntity(d, r, Core2026Proof.Add(r, "BOT_573"));
        var drawn = r.HandZone.Where(c => c.Card.IsSecret).ToArray();
        Verify(drawn.Length == 2, "BOT_573 draws one of each different Secret: Ice Barrier once, Counterspell once; drew " + string.Join(",", drawn.Select(c => c.Card.Id)));
    }

    // Unpinned_: no-duplicates conditions treat a Core copy and a classic copy of one card as a duplicate.
    private static void Unpinned_HighlanderAcrossPrintings()
    {
        var g = Fixture(); var p = g.Player1;
        var source = Core2026Proof.Add(p, "CS2_182");
        ToDeck(p, "CORE_EX1_289"); ToDeck(p, "EX1_287");
        Verify(SelfCondition.IsNoDupeInDeck.Eval(source), "two different Secrets are no duplicate");
        ToDeck(p, "EX1_289");
        Verify(!SelfCondition.IsNoDupeInDeck.Eval(source), "Core Ice Barrier and classic Ice Barrier are a duplicate");
    }

    // Unpinned_: a Discover never offers two printings of one card, and a short set still yields every different card.
    private static void Unpinned_DiscoverNeverOffersTwoPrintings()
    {
        var pool = new[] { "CORE_EX1_289", "EX1_289", "CORE_EX1_287", "EX1_287", "CORE_EX1_610", "EX1_610", "EX1_295" }.Select(Cards.FromId).ToArray();
        for (int seed = 1; seed <= 300; seed++)
        {
            var rnd = new Util.DeepCloneableRandom(seed);
            var offers = DiscoverTask.GetChoices(new[] { pool }, 3, rnd);
            Verify(offers.Length == 3 && offers.Select(c => c.Identity).Distinct().Count() == 3, "one set: three different cards (seed " + seed + "): " + string.Join(",", offers.Select(c => c.Id)));
            var rnd2 = new Util.DeepCloneableRandom(seed);
            var two = DiscoverTask.GetChoices(new[] { pool.Take(4).ToArray(), pool.Skip(4).ToArray() }, 3, rnd2);
            Verify(two.Select(c => c.Identity).Distinct().Count() == two.Length, "class and neutral sets: different cards (seed " + seed + "): " + string.Join(",", two.Select(c => c.Id)));
        }
        var shortSet = new[] { "CORE_EX1_289", "EX1_289", "EX1_287" }.Select(Cards.FromId).ToArray();
        var all = DiscoverTask.GetChoices(new[] { shortSet }, 3, new Util.DeepCloneableRandom(1));
        Verify(all.Length == 2, "three entries of two cards yield two choices: " + string.Join(",", all.Select(c => c.Id)));

        // The pinned pools: a sampled set of three never repeats a card, even from a pool that holds both printings.
        var g = Fixture(); var p = g.Player1;
        var filter = OfflinePoolFilter.Discover(type: CardType.SPELL, scope: PoolScope.Wild);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, 0);
        int pairs = candidates.Count(c => candidates.Any(o => o != c && o.IsSameCardAs(c)));
        Console.WriteLine($"INFO Unpinned_ReprintIdentity Wild spell pool: {candidates.Count} candidates, {pairs} of them have another printing in the pool");
        for (int i = 0; i < 400; i++)
        {
            var offers = OfflineCardPools.SampleDistinct(g, filter, p.HeroClass, 0);
            Verify(offers.Select(c => c.Identity).Distinct().Count() == offers.Length, "sampled offers are different cards: " + string.Join(",", offers.Select(c => c.Id)));
        }
    }

    // Unpinned_: "never roll the source" also leaves out the source's other printing, for random pools and offline candidate pools.
    private static void Unpinned_SourceExclusion()
    {
        var g = Fixture(); var p = g.Player1;
        var source = Core2026Proof.Add(p, "EX1_289");
        var list = new[] { "CORE_EX1_289", "EX1_289", "EX1_287" }.Select(Cards.FromId).ToArray();
        var pool = Cards.RandomPool(g, PoolDestination.Other, source);
        Verify(!pool.Legacy, "the fixture uses the pinned match pool");
        var left = pool.WithoutSource(list, source);
        Verify(left.Count == 1 && left[0].Id == "EX1_287", "WithoutSource leaves only Counterspell: " + string.Join(",", left.Select(c => c.Id)));
        var coreSource = Core2026Proof.Add(p, "CORE_EX1_289");
        left = pool.WithoutSource(list, coreSource);
        Verify(left.Count == 1 && left[0].Id == "EX1_287", "WithoutSource from the Core source leaves only Counterspell: " + string.Join(",", left.Select(c => c.Id)));

        var filter = OfflinePoolFilter.Discover(type: CardType.SPELL, scope: PoolScope.Wild);
        foreach (var src in new[] { Cards.FromId("EX1_289"), Cards.FromId("CORE_EX1_289") })
        {
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, src.AssetId);
            Verify(!candidates.Any(c => c.IsSameCardAs(src)), "Candidates exclude every printing of the source " + src.Id);
        }
    }

    // Unpinned_: a played card is the same card as its reprint in "cards you played this game" (Chromie, TIME_103).
    private static void Unpinned_PlayedHistoryAcrossPrintings()
    {
        var g = Fixture(); var p = g.Player1;
        Core2026Proof.Play(g, p, "CORE_EX1_289");
        ToDeck(p, "EX1_289");
        var chromie = Core2026Proof.Summon(p, "TIME_103");
        Core2026Proof.Kill(g, chromie);
        Verify(p.HandZone.Any(c => c.Card.Id == "EX1_289"), "Chromie draws the classic Ice Barrier after the Core one was played");
    }

    // The literal-id checks that name a card with reprints list every printing: Khadgar (DAL_575 and CORE_DAL_575).
    private static void Unpinned_LiteralNamesListEveryPrinting()
    {
        foreach (var id in new[] { "DAL_575", "CORE_DAL_575" })
        {
            var g = Fixture(); var p = g.Player1;
            Core2026Proof.Summon(p, id);
            Generic.SummonBlock(g, (Minion)Entity.FromCard(p, Cards.FromId("CS2_182")), -1, p.Hero);
            g.DeathProcessingAndAuraUpdate();
            Verify(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 2, id + " doubles a summon");
        }
    }

    // Unpinned_: Twisted Webweaver draws when a minion you already played is played again, in the other printing too.
    private static void Unpinned_PlayedMinionHistoryAcrossPrintings()
    {
        var g = Fixture(); var p = g.Player1;
        for (int i = 0; i < 5; i++) ToDeck(p, "EX1_289");
        Core2026Proof.Summon(p, Cards.FromAssetId(115612).Id);
        Core2026Proof.Play(g, p, "EX1_028");
        var second = Core2026Proof.Add(p, "CORE_EX1_028");
        int before = p.HandZone.Count;
        Core2026Proof.PlayEntity(g, p, second);
        Verify(p.HandZone.Count == before, "Twisted Webweaver draws for the Core Stranglethorn Tiger after the classic one was played: hand " + before + " -> " + p.HandZone.Count);
    }

    // Unpinned_: the Wild minion pool holds real pairs (Stranglethorn Tiger, Doomguard, Sleepy Dragon, Raptor Herald). Sampling three offers never
    // repeats a card, and the redraw branch runs: a plain replay of the same RNG would have picked a pair at least once.
    private static void Unpinned_MinionPoolSamplingRedraw()
    {
        var g = Fixture(); var p = g.Player1;
        var filter = OfflinePoolFilter.Discover(type: CardType.MINION, scope: PoolScope.Wild);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, 0);
        int paired = candidates.Count(c => candidates.Any(o => o != c && o.IsSameCardAs(c)));
        Verify(paired >= 4, "the Wild minion pool holds printings of one card: " + paired);
        int redraws = 0;
        for (int i = 0; i < 40000 && redraws < 5; i++)
        {
            var plain = g.Random.Clone();
            var indices = Enumerable.Range(0, candidates.Count).ToArray();
            var raw = new Card[3];
            for (int k = 0; k < 3; k++) { int j = k + plain.Next(indices.Length - k); (indices[k], indices[j]) = (indices[j], indices[k]); raw[k] = candidates[indices[k]]; }
            bool pair = raw.Select(c => c.Identity).Distinct().Count() < 3;
            var offers = OfflineCardPools.SampleDistinct(g, filter, p.HeroClass, 0);
            Verify(offers.Length == 3 && offers.Select(c => c.Identity).Distinct().Count() == 3, "three different cards: " + string.Join(",", offers.Select(c => c.Id)));
            if (pair) redraws++; else Verify(offers.SequenceEqual(raw), "without a repeated card the draws are the plain ones");
        }
        Verify(redraws >= 5, "the redraw branch ran " + redraws + " times");
    }

    // The identity data: how many cards link, no link joins different names, and known pairs resolve to one identity.
    private static void Unpinned_DataCheck()
    {
        var linked = CardIdentity.Linked;
        int sameName = 0;
        var bad = new List<string>();
        foreach (var (dbf, root) in linked)
        {
            var c = Cards.FromAssetId(dbf); var r = Cards.FromAssetId(root);
            Verify(c.Identity == root && r.Identity == root, c.Id + " resolves to the root " + r.Id);
            if (c.Name == r.Name) sameName++; else bad.Add(c.Id + " (" + c.Name + ") -> " + r.Id + " (" + r.Name + ")");
        }
        Verify(bad.Count == 0, "links join different names (" + bad.Count + "): " + string.Join("; ", bad.Take(60)));
        Verify(linked.Count > 100, "expected hundreds of linked cards, got " + linked.Count);
        foreach (var (a, b) in new[] { ("CORE_EX1_289", "EX1_289"), ("CORE_EX1_287", "EX1_287"), ("CORE_EX1_610", "EX1_610"), ("CORE_EX1_611", "EX1_611") })
            Verify(Cards.FromId(a).IsSameCardAs(Cards.FromId(b)) && Cards.FromId(b).IsSameCardAs(Cards.FromId(a)), a + " and " + b + " are one card");
        Verify(!Cards.FromId("EX1_289").IsSameCardAs(Cards.FromId("EX1_287")), "Ice Barrier and Counterspell are different cards");
        // Every collectible same-name same-type pair is one card (heroes excluded: skins share names). No group is a different card in our facts. Excluded: cards other proofs inject at run time (ids with _PROOF_, copies of real cards made after card load).
        var split = Cards.All.Where(c => c.Collectible && c.Type != CardType.HERO && !c.Id.Contains("_PROOF_")).GroupBy(c => (c.Name, c.Type)).Where(g => g.Select(c => c.Identity).Distinct().Count() > 1)
            .Select(g => g.Key.Name + ": " + string.Join(",", g.Select(c => c.Id))).ToArray();
        Verify(split.Length == 0, "collectible same-name same-type cards with different identities: " + string.Join("; ", split));
        int c1413 = Cards.All.Count(c => c[(GameTag)1413] != 0), c858 = Cards.All.Count(c => c[(GameTag)858] != 0);
        Console.WriteLine($"PASS Unpinned_ReprintIdentity data: {linked.Count} linked cards ({sameName} same-name, 0 cross-name) from tag 1413 on {c1413} and tag 858 on {c858} rows; {linked.Values.Distinct().Count()} roots");
    }
}
