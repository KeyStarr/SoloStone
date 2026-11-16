using System.Diagnostics;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>
/// Zephrys the Great (ULD_003). Retail's wish logic is server-side and unpublished, so the acceptance bar is the documented
/// behaviour: the duplicate gate, a private non-Discover choice, determinism without the live RNG, public information only,
/// and the offers the wiki (Template:Zephrys_the_Great_notes) and the patch notes describe.
/// </summary>
internal static class ZephrysTheGreatProof
{
    private const string Zephrys = "ULD_003";
    // Ten different vanilla minions: a deck without duplicates.
    private static readonly string[] Highlander = { "CS2_120", "CS2_168", "CS2_171", "CS2_172", "CS2_179", "CS2_182", "CS2_186",
        "CS2_200", "CS2_201", "CS2_213" };

    [GroupProof("zephrys-the-great", ProofKind.Cards)]
    internal static void Run()
    {
        long failures = ZephrysWish.Failures;
        Registration();
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = New(seed: seed); var p = g.Player1;
            var offers = Wish(g, p, 8);
            Core2026Proof.Check(offers.Length == ZephrysWish.Offered && offers.Distinct().Count() == offers.Length, "three distinct offers");
            int chosen = p.Choice.Choices[1];
            string pick = g.IdEntityDic[chosen].Card.Id;
            // The open choice survives an exact clone and a snapshot, and each resumes to the same state.
            PoolPilotProof.Resume(g, next => CoreCostProof.Answer(next, chosen));
            Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 1 && p.HandZone[0].Card.Id == pick, "the wish goes to hand");
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(g.CloneExact(history: true)), "exact clone after the wish");
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)),
                "snapshot after the wish");
        }
        Gate();
        Choice();
        Determinism();
        PublicOnly();
        Documented();
        Pool();
        Budget();
        Estimate();
        OffTurn();
        Shudderwock();
        Stress();
        Core2026Proof.Check(ZephrysWish.Failures == failures, "no wish simulation failed: " + (ZephrysWish.Failures - failures));
    }

    // ---- Fixture ----

    internal static Game New(CardClass mine = CardClass.MAGE, CardClass theirs = CardClass.WARRIOR, int seed = 7)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true,
            Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = mine, Player2HeroClass = theirs,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 })
            foreach (var e in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(e));
        Deck(g.Player1, Highlander);
        return g;
    }

    internal static void Deck(Controller p, IEnumerable<string> ids)
    {
        foreach (var e in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(e));
        foreach (var id in ids) Entity.FromCard(p, Cards.FromId(id), null, p.DeckZone);
    }

    internal static Minion Ready(Controller p, string id)
    {
        var m = Summon(p, id);
        m.IsExhausted = false;
        return m;
    }

    /// <summary>Plays Zephrys with <paramref name="manaLeft"/> mana after paying for him; returns the offered ids.</summary>
    internal static string[] Wish(Game g, Controller p, int manaLeft)
    {
        var zephrys = Add(p, Zephrys);
        p.BaseMana = Math.Min(10, manaLeft + zephrys.Cost); p.UsedMana = 0; p.TemporaryMana = Math.Max(0, manaLeft + zephrys.Cost - 10);
        var task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == zephrys.Id);
        Core2026Proof.Check(g.Process(task), "Zephrys played");
        return p.Choice == null ? Array.Empty<string>() : p.Choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToArray();
    }

    private static string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => Cards.FromId(id).Name));

    // ---- Cases ----

    private static void Registration()
    {
        var card = Cards.FromId(Zephrys);
        Core2026Proof.Check(card.Implemented && card.ExcludeFromLegacyRandomPools
            && SabberStoneCore.CardSets.Client.EffectModules.ByDbfId.GetValueOrDefault(card.AssetId) == Zephrys,
            "registered Wild module, kept out of legacy random pools");
        Core2026Proof.Check(!SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(Zephrys),
            "not part of the Standard corpus (stress decks, AI card frequencies)");
        Core2026Proof.Check(Cards.IsFormatLegal(card, FormatType.FT_WILD), "Wild legal");
        var export = ClientExport.TryCards();
        if (export == null) ClientExport.Skip("Zephrys identity and stats against the client export");
        else
        {
            var data = export[Zephrys];
            Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Cost == data.GetProperty("cost").GetInt32()
                && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32(),
                "Zephrys identity and stats match the client");
        }
    }

    private static void Gate()
    {
        var g = New(); var p = g.Player1;
        Deck(p, Highlander.Append(Highlander[0]));
        Core2026Proof.Check(Wish(g, p, 8).Length == 0 && p.Choice == null && p.HandZone.Count == 0, "a duplicate in the deck: no wish");

        g = New(); p = g.Player1;
        Deck(p, Array.Empty<string>());
        Core2026Proof.Check(Wish(g, p, 8).Length == ZephrysWish.Offered, "an empty deck has no duplicates");

        g = New(); p = g.Player1;
        Add(p, Highlander[0]); Add(p, Highlander[0]);
        Core2026Proof.Check(Wish(g, p, 8).Length == ZephrysWish.Offered, "duplicates in hand do not matter");

        g = New(); p = g.Player1;
        var zephrys = Add(p, Zephrys);
        Core2026Proof.Check(PoweredUp.IsMet(zephrys), "glows with a duplicate-free deck");
        Entity.FromCard(p, Cards.FromId(Highlander[0]), null, p.DeckZone);
        Core2026Proof.Check(!PoweredUp.IsMet(zephrys), "no glow with a duplicate");
    }

    private static void Choice()
    {
        var g = New(); var p = g.Player1;
        Wish(g, p, 8);
        foreach (int id in p.Choice.Choices)
            Core2026Proof.Check(g.IdEntityDic[id][Generic.PrivateDecisionOfferTag] == p.PlayerId, "offers are private");
        Core2026Proof.Check(p.Choice.HistoryDiscoverId == 0, "a wish is not a Discover");
        CoreCostProof.Answer(g, p.Choice.Choices[0]);

        // Full hand: the wish is burned like any card added to a full hand.
        g = New(); p = g.Player1;
        PoolPilotProof.FillHand(p, 9);
        Wish(g, p, 8);
        CoreCostProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.HandZone.Count == 10, "the wish fills the hand");

        // Brann Bronzebeard: two separate wishes.
        g = New(); p = g.Player1;
        Ready(p, "LOE_077");
        Wish(g, p, 8);
        Core2026Proof.Check(p.Choice != null, "first wish");
        CoreCostProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == ZephrysWish.Offered, "second wish with Brann");
        CoreCostProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 2, "two wishes in hand");
    }

    private static void Determinism()
    {
        string[] Run()
        {
            var g = New(); var p = g.Player1;
            Ready(p, "CS2_182"); Ready(p.Opponent, "CS2_120"); Ready(p.Opponent, "EX1_066");
            long draws = g.Random.DrawCount;
            var offers = Wish(g, p, 6);
            Core2026Proof.Check(g.Random.DrawCount == draws, "the wish draws nothing from the live RNG");
            return offers;
        }
        Core2026Proof.Check(Run().SequenceEqual(Run()), "same state, same offers");
    }

    private static void PublicOnly()
    {
        // Hidden cards never change the offers: the same board with different hands, decks and Secret identities.
        string[] Offers(string[] hand, string secret)
        {
            var g = New(); var p = g.Player1;
            foreach (var id in hand) Add(p, id);
            Ready(p.Opponent, "CS2_182");
            Add(p.Opponent, "CS2_029");
            var s = Add(p.Opponent, secret);
            p.Opponent.SecretZone.Add((Spell)p.Opponent.HandZone.Remove(s));
            return Wish(g, p, 6);
        }
        var a = Offers(new[] { "CS2_029", "CS2_024" }, "EX1_295");
        var b = Offers(new[] { "CS2_120", "CS2_168" }, "EX1_289");
        Core2026Proof.Check(a.SequenceEqual(b), "hand contents and Secret identities are hidden: " + Names(a) + " vs " + Names(b));
    }

    private static void Expect(string name, string[] offers, string must, string mustNot = null)
    {
        Console.WriteLine("Zephrys " + name + ": " + Names(offers));
        if (must != null) Core2026Proof.Check(offers.Contains(must), name + ": expected " + Cards.FromId(must).Name + ", got " + Names(offers));
        if (mustNot != null) Core2026Proof.Check(!offers.Contains(mustNot), name + ": did not expect " + Cards.FromId(mustNot).Name);
    }

    private static void Documented()
    {
        // Mage, enemy at 9, 8 mana left: Avenging Wrath + Fireblast is lethal (wiki).
        var g = New(CardClass.MAGE); var p = g.Player1;
        p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 9;
        Expect("lethal: Avenging Wrath + Fireblast", Wish(g, p, 8), "EX1_384");

        // Hunter, enemy at 8, 7 mana: Fireball + Steady Shot.
        g = New(CardClass.HUNTER); p = g.Player1;
        p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 8;
        Expect("lethal: Fireball + Steady Shot", Wish(g, p, 7), "CS2_029");

        // A Taunt in the way of lethal: Silence it.
        g = New(CardClass.WARRIOR); p = g.Player1;
        Ready(p, "CS2_182"); Ready(p, "CS2_200");
        Ready(p.Opponent, "CS2_179");
        p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 10;
        Expect("lethal through a Taunt", Wish(g, p, 1), "EX1_332");

        // Four or more enemy minions with 4 or less Health, 7 mana: Flamestrike.
        g = New(); p = g.Player1;
        foreach (var id in new[] { "CS2_120", "CS2_172", "CS2_168", "CS2_171" }) Ready(p.Opponent, id);
        Expect("wide board", Wish(g, p, 7), "CS2_032");

        // Enemy weapon: Acidic Swamp Ooze.
        g = New(); p = g.Player1;
        p.Opponent.Hero.Weapon = null;
        var axe = (Weapon)Entity.FromCard(p.Opponent, Cards.FromId("CS2_112"));
        p.Opponent.Hero.AddWeapon(axe);
        Expect("enemy weapon", Wish(g, p, 3), "EX1_066");

        // Enemy Secret: Flare.
        g = New(); p = g.Player1;
        var secret = Add(p.Opponent, "EX1_295");
        p.Opponent.SecretZone.Add((Spell)p.Opponent.HandZone.Remove(secret));
        Expect("enemy Secret", Wish(g, p, 3), "EX1_544");

        // Turn 2, empty boards: proactive cards.
        g = New(CardClass.HUNTER); p = g.Player1;
        var early = Wish(g, p, 3);
        Console.WriteLine("Zephrys early empty board: " + Names(early));
        Core2026Proof.Check(early.Count(id => ZephrysPool.Entries.Single(e => e.Id == id).Category is WishCategory.Proactive or WishCategory.DrawGen) >= 2,
            "early empty board: mostly proactive");

        // Empty deck: no card draw.
        g = New(); p = g.Player1;
        Deck(p, Array.Empty<string>());
        var dry = Wish(g, p, 8);
        Core2026Proof.Check(dry.All(id => !ZephrysPool.Entries.Single(e => e.Id == id).Flags.HasFlag(WishFlags.Draws)), "empty deck: no draw: " + Names(dry));

        // Doomsayer: Crazed Alchemist against the enemy's, never on your own.
        g = New(); p = g.Player1;
        Ready(p, "CS2_182"); Ready(p, "CS2_200");
        Ready(p.Opponent, "NEW1_021");
        Expect("enemy Doomsayer", Wish(g, p, 2), "EX1_059");

        // A single enemy minion: never Starfall.
        g = New(CardClass.DRUID); p = g.Player1;
        Ready(p.Opponent, "CS2_200");
        Expect("single minion", Wish(g, p, 8), null, "NEW1_007");

        // Low health facing a big board: a survival card, next to an answer to the board.
        g = New(CardClass.DRUID); p = g.Player1;
        p.Hero.Damage = p.Hero.BaseHealth - 6;
        foreach (var id in new[] { "CS2_200", "CS2_201" }) Ready(p.Opponent, id);
        var low = Wish(g, p, 6);
        Console.WriteLine("Zephrys low health: " + Names(low));
        Core2026Proof.Check(low.Any(id => Category(id) == WishCategory.Survival), "low health: a survival card: " + Names(low));
        Core2026Proof.Check(low.Any(id => Category(id) is WishCategory.BoardClear or WishCategory.Removal), "low health: an answer: " + Names(low));

        // Low health, little mana, nothing on the enemy board: healing.
        g = New(CardClass.DRUID); p = g.Player1;
        p.Hero.Damage = p.Hero.BaseHealth - 5;
        Expect("low health, 3 mana", Wish(g, p, 3), "CS2_007");

        // Full health against a weapon: no survival card (Mirror Image is no perfect card here).
        g = New(); p = g.Player1;
        p.Opponent.Hero.Weapon = null;
        p.Opponent.Hero.AddWeapon((Weapon)Entity.FromCard(p.Opponent, Cards.FromId("CS2_112")));
        var armed = Wish(g, p, 3);
        Console.WriteLine("Zephrys full health against a weapon: " + Names(armed));
        Core2026Proof.Check(armed.All(id => Category(id) != WishCategory.Survival), "full health: no survival card: " + Names(armed));

        // One big threat: single-target removal.
        g = New(CardClass.SHAMAN); p = g.Player1;
        Ready(p.Opponent, "CS2_201");
        var threat = Wish(g, p, 4);
        Console.WriteLine("Zephrys one big threat: " + Names(threat));
        Core2026Proof.Check(threat.Any(id => Category(id) == WishCategory.Removal), "one big threat: removal: " + Names(threat));

        // Late game, empty boards: a big proactive card.
        g = New(CardClass.PALADIN); p = g.Player1;
        Expect("late game, empty boards", Wish(g, p, 8), "EX1_383");
    }

    private static WishCategory Category(string id) => ZephrysPool.Entries.Single(e => e.Id == id).Category;

    private static void Pool()
    {
        var ids = ZephrysPool.Entries.Select(e => e.Id).ToArray();
        Core2026Proof.Check(ids.Distinct().Count() == ids.Length, "pool ids unique");
        Core2026Proof.Check(ids.All(id => Cards.FromId(id) is { Collectible: true } c && (c.Set == CardSet.CORE || c.Set == CardSet.EXPERT1)),
            "pool is Basic and Classic");
        // Never in the pool: Hall of Fame cards (wiki roster), return-to-hand cards, Zephrys himself.
        foreach (var id in new[] { "CS2_235", "CS2_236", "EX1_007", "EX1_048", "EX1_085", "EX1_105", "EX1_116", "EX1_350", "EX1_591",
                     "EX1_624", "EX1_625", "EX1_144", "EX1_049", "EX1_057", Zephrys })
            Core2026Proof.Check(!ids.Contains(id), "excluded from the pool: " + id);
        Core2026Proof.Check(ids.All(id => Cards.FromId(id).Implemented || Cards.FromId(id).ChooseOne), "pool cards implemented");
        Console.WriteLine("Zephrys pool: " + ids.Length + " cards");
    }

    private static void Budget()
    {
        // Worst case: two full boards and 10 mana.
        var g = New(CardClass.MAGE); var p = g.Player1;
        foreach (var id in new[] { "CS2_120", "CS2_168", "CS2_171", "CS2_172", "CS2_179", "CS2_182" }) Ready(p, id);
        foreach (var id in new[] { "CS2_120", "CS2_168", "CS2_171", "CS2_172", "CS2_179", "CS2_182", "CS2_200" }) Ready(p.Opponent, id);
        var watch = Stopwatch.StartNew();
        var offers = Wish(g, p, 10);
        watch.Stop();
        Console.WriteLine("Zephrys worst case: " + watch.ElapsedMilliseconds + " ms: " + Names(offers));
        Core2026Proof.Check(watch.ElapsedMilliseconds < 5000, "wish within a generous time limit");
    }

    private static Game WorstCase()
    {
        var g = New(CardClass.MAGE); var p = g.Player1;
        foreach (var id in new[] { "CS2_120", "CS2_168", "CS2_171", "CS2_172", "CS2_179", "CS2_182" }) Ready(p, id);
        foreach (var id in new[] { "CS2_120", "CS2_168", "CS2_171", "CS2_172", "CS2_179", "CS2_182", "CS2_200" }) Ready(p.Opponent, id);
        return g;
    }

    private static void Estimate()
    {
        // An NPC's search copy estimates; the mark reaches every clone of the copy and never the live game.
        var g = WorstCase();
        var copy = g.CloneExact(history: true);
        copy.MarkSearchCopy();
        Core2026Proof.Check(!g.IsSearchCopy && copy.IsSearchCopy && copy.CloneExact().IsSearchCopy
            && copy.CloneForSimulation(1).IsSearchCopy && !g.CloneExact().IsSearchCopy, "search copies and their clones only");

        string[] Run(Game game, out long ms)
        {
            var watch = Stopwatch.StartNew();
            var offers = Wish(game, game.Player1, 4);
            ms = watch.ElapsedMilliseconds;
            return offers;
        }
        var estimate = Run(copy, out long fast);
        var again = g.CloneExact(history: true); again.MarkSearchCopy();
        Core2026Proof.Check(Run(again, out _).SequenceEqual(estimate), "an estimate is deterministic");
        var exact = Run(g, out long slow);
        Console.WriteLine("Zephrys estimate " + fast + " ms: " + Names(estimate) + "; exact " + slow + " ms: " + Names(exact));
        Core2026Proof.Check(estimate.Length == ZephrysWish.Offered && estimate.Distinct().Count() == estimate.Length,
            "an estimate offers three distinct cards");
        Core2026Proof.Check(estimate.All(id => Cards.FromId(id).Cost <= 4 || id == "EX1_586"), "an estimate offers this turn's cards only");
    }

    private static void OffTurn()
    {
        // Off its owner's turn there is no turn to plan: cheap eligible cards, no simulation.
        var g = New(); var p = g.Player2;
        p.BaseMana = 3; p.UsedMana = 0;
        var zephrys = Add(p, Zephrys);
        var offers = ZephrysWish.Offers(g, p, zephrys);
        Console.WriteLine("Zephrys off turn: " + Names(offers.Select(c => c.Id)));
        Core2026Proof.Check(offers.Length == ZephrysWish.Offered && offers.Distinct().Count() == offers.Length
            && offers.All(c => c.Cost <= 3 && ZephrysPool.Entries.Any(e => e.Id == c.Id)), "off-turn offers: three cheap pool cards");
    }

    private static void Shudderwock()
    {
        // Shudderwock repeats the wish: the recipe runs again, mid-Battlecry, and picks one offer at random.
        var g = New(CardClass.SHAMAN); var p = g.Player1;
        Wish(g, p, 8);
        CoreCostProof.Answer(g, p.Choice.Choices[0]);
        var shudderwock = Add(p, "GIL_820");
        p.BaseMana = 10; p.UsedMana = 0;
        var play = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == shudderwock.Id);
        Core2026Proof.Check(g.Process(play), "Shudderwock played");
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 2
            && p.HandZone.All(c => ZephrysPool.Entries.Any(e => e.Id == c.Card.Id)), "Shudderwock repeats the wish into hand");
    }

    private static void Stress()
    {
        // Mixed boards with auras, Deathrattles, damage triggers, Secrets and weapons: every wish completes, offers stay valid.
        string[] minions = { "EX1_097", "NEW1_019", "NEW1_020", "EX1_591", "EX1_350", "CS2_222", "EX1_012", "EX1_556", "EX1_008",
            "EX1_023", "EX1_563", "EX1_614", "CS2_042", "EX1_584", "EX1_565", "CS2_201", "EX1_383", "EX1_110", "EX1_001", "NEW1_021",
            "CS2_179", "CS2_182", "CS2_120" };
        CardClass[] classes = { CardClass.MAGE, CardClass.HUNTER, CardClass.PRIEST, CardClass.ROGUE, CardClass.WARLOCK,
            CardClass.WARRIOR, CardClass.SHAMAN, CardClass.PALADIN, CardClass.DRUID, CardClass.DEMONHUNTER };
        var watch = Stopwatch.StartNew();
        for (int seed = 1; seed <= 20; seed++)
        {
            var rng = new Random(seed);
            var g = New(classes[seed % classes.Length], classes[(seed * 3) % classes.Length], seed); var p = g.Player1;
            for (int i = rng.Next(0, 7); i > 0; i--) Ready(p, minions[rng.Next(minions.Length)]);
            for (int i = rng.Next(0, 8); i > 0; i--) Ready(p.Opponent, minions[rng.Next(minions.Length)]);
            for (int i = rng.Next(0, 6); i > 0; i--) Add(p.Opponent, "CS2_182");
            if (rng.Next(3) == 0)
            {
                var secret = Add(p.Opponent, new[] { "EX1_295", "EX1_289", "EX1_610", "EX1_130" }[rng.Next(4)]);
                p.Opponent.SecretZone.Add((Spell)p.Opponent.HandZone.Remove(secret));
            }
            if (rng.Next(3) == 0)
            {
                p.Opponent.Hero.Weapon = null;
                p.Opponent.Hero.AddWeapon((Weapon)Entity.FromCard(p.Opponent, Cards.FromId("CS2_112")));
            }
            p.Hero.Damage = rng.Next(0, 25);
            p.Opponent.Hero.Damage = rng.Next(0, 25);
            var offers = Wish(g, p, rng.Next(0, 9));
            Core2026Proof.Check(offers.Length <= ZephrysWish.Offered && offers.Distinct().Count() == offers.Length
                && offers.All(id => ZephrysPool.Entries.Any(e => e.Id == id)), "stress " + seed + ": valid offers: " + Names(offers));
            if (offers.Length > 0) CoreCostProof.Answer(g, p.Choice.Choices[0]);
        }
        Console.WriteLine("Zephrys stress: 20 wishes in " + watch.ElapsedMilliseconds + " ms");
    }
}
