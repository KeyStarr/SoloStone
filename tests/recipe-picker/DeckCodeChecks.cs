using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Deck-code import (#140): the plan (what a pasted code becomes) and the atomic profile create. Client-free.
internal static class DeckCodeChecks
{
    private static int checks;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
    private static void Refuses(Action action, DeckCodeRefusalReason reason, string label)
    {
        try { action(); }
        catch (DeckCodeRefusal refusal) when (refusal.Reason == reason) { Check(true, label); return; }
        catch (DeckCodeRefusal refusal) { throw new Exception("FAIL " + label + ": refused as " + refusal.Reason); }
        throw new Exception("FAIL accepted " + label);
    }
    private static void Rejects(Action action, string label)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is IOException)
        { Check(true, label); return; }
        throw new Exception("FAIL accepted " + label);
    }

    private const string Mage = "HERO_08", Warrior = "HERO_01";
    private static readonly SabberStoneCore.Enums.CardClass MageClass = LocalDecks.HeroClass(Mage), WarriorClass = LocalDecks.HeroClass(Warrior);

    private static IEnumerable<Card> Pool => LocalDecks.CustomCards.Where(card => LocalDecks.IsCustomCard(card.Id));
    private static Card First(Func<Card, bool> where, string what) => Pool.Where(where).OrderBy(card => card.Id, StringComparer.Ordinal).FirstOrDefault()
        ?? throw new Exception("FAIL fixture: no " + what + " in the active pool");
    private static IEnumerable<(string, int)> Main(params (string id, int qty)[] rows) => rows;
    private static DeckCodePlan Plan(string hero, IEnumerable<(string, int)> main, IEnumerable<(string, string, int)> side = null, string name = "Imported")
        => NativeDeckCodeImport.Plan(hero, name, "New deck", main, side);

    internal static int Run()
    {
        var neutral = Pool.Where(card => card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL && card.Rarity != Rarity.LEGENDARY
            && !LocalCardCatalog.IsCore(card.Id) && LocalDecks.FabledBundle(card.Id).Count == 0 && !LocalDecks.IsUnsupportedSideboardOwner(card.Id)
            && LocalDecks.DeckSize(new[] { card.Id }) == 30).OrderBy(card => card.Id, StringComparer.Ordinal).Take(20).Select(card => card.Id).ToArray();
        Check(neutral.Length == 20, "fixture: twenty plain neutral cards");
        var legendary = First(card => card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL && card.Rarity == Rarity.LEGENDARY && !LocalCardCatalog.IsCore(card.Id)
            && LocalDecks.FabledBundle(card.Id).Count == 0 && !LocalDecks.IsUnsupportedSideboardOwner(card.Id) && LocalDecks.DeckSize(new[] { card.Id }) == 30, "neutral legendary").Id;
        var warriorOnly = First(card => card.Class == WarriorClass && card.Rarity != Rarity.LEGENDARY && !LocalCardCatalog.IsCore(card.Id), "warrior card").Id;
        var mageOnly = First(card => card.Class == MageClass && card.Rarity != Rarity.LEGENDARY && !LocalCardCatalog.IsCore(card.Id), "mage card").Id;
        string notInPool = Cards.All.Where(card => card.Implemented && !LocalDecks.IsCustomCard(card.Id)).Select(card => card.Id).OrderBy(id => id, StringComparer.Ordinal).First();
        Console.WriteLine("fixtures: legendary=" + legendary + " warrior=" + warriorOnly + " mage=" + mageOnly + " outside=" + notInPool);

        // Plain import: every usable card, in order, nothing dropped, nothing to say.
        var plain = Plan(Mage, Main(neutral.Take(15).Select(id => (id, 2)).ToArray()));
        Check(plain.Cards.Count == 30 && plain.Dropped == 0 && plain.Name == "Imported" && plain.Hero == Mage
            && NativeDeckCodeImport.StatusMessage(plain, 0) == null, "a plain code imports whole and says nothing");
        Check(NativeDeckCodeImport.StatusMessage(plain, 3) == "Deck imported. 3 cards are not in your collection yet."
            && NativeDeckCodeImport.StatusMessage(plain, 1) == "Deck imported. 1 card is not in your collection yet.", "missing cards are told plainly");

        // Unusable cards are dropped and counted, the rest imported (owner ruling 1).
        var withUnknown = Plan(Mage, Main((neutral[0], 2), (null, 1), ("NOT_A_CARD", 2), (notInPool, 2), (neutral[1], 2)));
        Check(withUnknown.Cards.SequenceEqual(new[] { neutral[0], neutral[0], neutral[1], neutral[1] }) && withUnknown.Unusable == 5 && withUnknown.WrongClass == 0,
            "unknown ids, untranslatable ids and cards outside the pool are dropped and counted");
        Check(NativeDeckCodeImport.StatusMessage(withUnknown, 0) == "Deck imported. Left out: 5 cards that cannot be used here.", "dropped cards are told once, with the count");

        // Wrong class.
        var wrong = Plan(Mage, Main((neutral[0], 2), (warriorOnly, 2), (mageOnly, 2)));
        Check(wrong.Cards.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { neutral[0], neutral[0], mageOnly, mageOnly }.OrderBy(id => id, StringComparer.Ordinal))
            && wrong.WrongClass == 2, "cards of another class are dropped and counted; neutral and own-class cards stay");

        // Copy limits: a Legendary once, others twice, across printings.
        var trim = Plan(Mage, Main((legendary, 2), (neutral[0], 4)));
        Check(trim.Cards.Count(id => id == legendary) == 1 && trim.Cards.Count(id => id == neutral[0]) == 2 && trim.OverLimit == 3,
            "a Legendary is trimmed to one copy and other cards to two");
        var pair = LocalCoreProgression.ById.Values.FirstOrDefault(core => core.Id != core.CopyGroup && LocalDecks.IsCustomCard(core.Id) && LocalDecks.IsCustomCard(core.CopyGroup)
            && Cards.FromId(core.Id).Rarity != Rarity.LEGENDARY && LocalDecks.IsAllowedInDeck(Cards.FromId(core.Id), MageClass))
            ?? throw new Exception("FAIL fixture: no card with two printings");
        var printings = Plan(Mage, Main((pair.Id, 2), (pair.CopyGroup, 2)));
        Check(printings.Cards.Count == 2 && printings.OverLimit == 2 && LocalCardCatalog.WithinCopyLimits(printings.Cards),
            "two printings of one card share the copy limit (" + pair.Id + "/" + pair.CopyGroup + ")");

        // Fabled: the primary brings its companions; listed companions are not counted twice.
        var fabled = Pool.FirstOrDefault(card => LocalDecks.FabledBundle(card.Id).Count > 0 && card.Id != "TIME_005" && LocalDecks.IsAllowedInDeck(card, MageClass)
            && LocalDecks.DeckSize(new[] { card.Id }) == 30);
        if (fabled != null)
        {
            var bundle = LocalDecks.FabledBundle(fabled.Id);
            var fabledPlan = Plan(Mage, Main((fabled.Id, 1), (bundle[0], 1), (neutral[0], 2)));
            Check(fabledPlan.Cards.Contains(fabled.Id) && bundle.All(fabledPlan.Cards.Contains) && fabledPlan.Cards.Count == 3 + bundle.Count && fabledPlan.Dropped == 0,
                "a Fabled primary brings exactly its companions: " + fabled.Id);
            var fabledOnly = Plan(Mage, Main((fabled.Id, 1)));
            Check(fabledOnly.Cards.Count == 1 + bundle.Count, "a Fabled primary listed alone is expanded");
        }
        else Console.WriteLine("NOTE no Fabled primary in the active pool for this class; Fabled expansion checked through the Rafaam deck-size rule only");

        // Sideboards: Beatrix (Paladin, one slot) and King of the Underbelly (Hunter, three) are modelled.
        const string Paladin = "HERO_04", Hunter = "HERO_05";
        string beatrix = "JAIL_397", underbelly = "JAIL_831";
        Check(LocalDecks.IsCustomCard(beatrix) && LocalDecks.IsCustomCard(underbelly), "fixture: both modelled sideboard owners are in the active pool");
        string Eligible(string source, params string[] not) => Pool.Where(card => LocalDecks.SideboardEligible(source, card.Id) && card.Id != source
            && !neutral.Contains(card.Id) && !not.Contains(card.Id)).OrderBy(card => card.Id, StringComparer.Ordinal).First().Id;
        string pick = Eligible(beatrix);
        var withBoard = Plan(Paladin, Main((beatrix, 1), (neutral[0], 2)), new[] { (beatrix, pick, 1) });
        Check(withBoard.Sideboards.Count == 1 && withBoard.Sideboards[0].Source == beatrix && withBoard.Sideboards[0].Cards.SequenceEqual(new[] { pick })
            && withBoard.SideboardCardsDropped == 0, "a modelled sideboard (Beatrix) is kept whole");
        var tooMany = Plan(Paladin, Main((beatrix, 1), (neutral[0], 2)), new[] { (beatrix, pick, 2) });
        Check(tooMany.Sideboards.Single().Cards.Count == 1 && tooMany.SideboardCardsDropped == 1, "sideboard cards beyond the owner's slots are dropped");
        var ownerless = Plan(Paladin, Main((neutral[0], 2)), new[] { (beatrix, pick, 1) });
        Check(ownerless.Sideboards.Count == 0 && ownerless.SideboardCardsDropped == 1, "a sideboard whose owner is not in the deck is dropped");
        var badCard = Plan(Paladin, Main((beatrix, 1), (neutral[0], 2)), new[] { (beatrix, neutral[1], 1) });
        Check(badCard.Sideboards.Count == 0 && badCard.SideboardCardsDropped == 1, "an ineligible sideboard card drops its board");
        Check(NativeDeckCodeImport.StatusMessage(badCard, 0).Contains("1 card from a sideboard"), "a dropped sideboard is told");
        var inMain = Plan(Paladin, Main((beatrix, 1), (pick, 1), (neutral[0], 2)), new[] { (beatrix, pick, 1) });
        Check(inMain.Sideboards.Count == 0 && inMain.SideboardCardsDropped == 1 && inMain.Cards.Contains(pick), "a Beatrix pick that is also in the main deck drops the board, the main card stays");
        string beast1 = Eligible(underbelly), beast2 = Eligible(underbelly, beast1), beast3 = Eligible(underbelly, beast1, beast2);
        var trio = Plan(Hunter, Main((underbelly, 1), (neutral[0], 2)), new[] { (underbelly, beast1, 1), (underbelly, beast2, 1), (underbelly, beast3, 1) });
        Check(trio.Sideboards.Single().Source == underbelly && trio.Sideboards.Single().Cards.Count == 3 && trio.SideboardCardsDropped == 0,
            "King of the Underbelly keeps its three picks");
        // E.T.C. Band Manager and Zilliax Deluxe 3000 own sideboards in the client; this engine has neither, so each is an unusable card:
        // it and its sideboard are dropped and the rest of the deck is imported. (A modelled-engine owner the profile refuses in a main
        // deck is dropped by LocalDecks.IsUnsupportedSideboardOwner the same way.)
        var unsupported = Plan(Mage, Main(("ETC_080", 1), ("TOY_330", 1), (neutral[0], 2)),
            new[] { ("ETC_080", neutral[1], 1), ("TOY_330", neutral[2], 2) });
        Check(unsupported.Cards.SequenceEqual(new[] { neutral[0], neutral[0] }) && unsupported.Sideboards.Count == 0
            && unsupported.SideboardCardsDropped == 3 && unsupported.Unusable == 2, "E.T.C. and Zilliax owners are dropped with their sideboards");

        // Expansion pack cards (not in the legacy custom pool) are usable: the editor and the profile accept them (#140 review B1).
        var pack0 = Cards.All.Where(card => LocalShop.IsStandardCard(card) && LocalProfile.KnownLedgerCard(card.Id) && !LocalDecks.IsCustomCard(card.Id) && card.Rarity != Rarity.LEGENDARY
            && (card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL || card.Class == MageClass) && LocalDecks.FabledBundle(card.Id).Count == 0
            && !LocalDecks.HasModelledSideboard(card.Id) && !LocalDecks.IsUnsupportedSideboardOwner(card.Id) && LocalDecks.DeckSize(new[] { card.Id }) == 30).GroupBy(card => card.Set).OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(card => card.Id, StringComparer.Ordinal).Take(3)).Select(card => card.Id).Distinct().ToArray();
        Console.WriteLine("pack candidates " + pack0.Length);
        var pack = pack0.Take(15).ToArray();
        Check(pack.Length == 15 && pack.Select(id => Cards.FromId(id).Set).Distinct().Count() >= 4, "fixture: fifteen pack cards across at least four sets: " + string.Join(",", pack));
        var packPlan = Plan(Mage, Main(pack.Select(id => (id, 2)).ToArray()));
        Check(packPlan.Cards.Count == 30 && packPlan.Dropped == 0 && NativeDeckCodeImport.StatusMessage(packPlan, 0) == null, "a full thirty-card pack-card deck imports with nothing dropped");
        Check(Plan(Mage, Main((pack[0], 2), (pack[1], 2), (warriorOnly, 1), ("NOT_A_CARD", 1))).Cards.Count == 4, "pack cards mix with the rest of a code");

        // Names fit the saved-deck limit in UTF-16 code units: emoji (surrogate pairs) cannot fail the commit.
        string emoji = string.Concat(Enumerable.Repeat("\U0001F600", 20));
        var emojiPlan = Plan(Mage, Main((neutral[0], 2)), name: emoji);
        Check(emojiPlan.Name.Length <= 32 && emojiPlan.Name.Length > 0 && emojiPlan.Name != "New deck", "an emoji name is cut to 32 UTF-16 code units");
        Check(NativeDeckCodeImport.Name(Mage, "a" + string.Concat(Enumerable.Repeat("\U0001F600", 16)), "New deck").Length <= 32, "a name never splits a surrogate pair");

        // A Fabled companion listed without its primary is dropped and counted.
        if (fabled != null)
        {
            var orphan = Plan(Mage, Main((LocalDecks.FabledBundle(fabled.Id)[0], 1), (neutral[0], 2)));
            Check(orphan.Cards.Count == 2 && orphan.Unusable == 1, "a Fabled companion without its primary is dropped and counted");
        }

        // The native fill after an import is skipped silently, once, only for that deck; any other edit paste speaks.
        var skip = new NativeDeckCodeImport.NativeFillSkip();
        skip.Arm(7);
        Check(!skip.TryConsume(true, 8) && skip.TryConsume(true, 7) && !skip.TryConsume(true, 7), "the native fill is skipped only for the imported deck, once; a real edit paste is not");
        skip.Arm(7);
        Check(!skip.TryConsume(false, 7) && !skip.TryConsume(true, 7), "leaving the Collection drops the skip");
        skip.Arm(7); skip.Reset();
        Check(!skip.TryConsume(true, 7), "the next import resets the skip");

        // Tokens, uncollectible and hero cards are unusable: every uncollectible card in the catalogue, CATA_190h included.
        var uncollectible = Cards.All.Where(card => !card.Collectible).Select(card => card.Id).ToList();
        Check(uncollectible.Count > 200 && uncollectible.All(id => !NativeDeckCodeImport.Usable(id)),
            "every uncollectible card in the catalogue is unusable: " + uncollectible.Count);
        // CATA_190h (Deathwing, Worldbreaker) is a collectible Standard hero card the editor accepts: the import matches the editor.
        Check(NativeDeckCodeImport.Usable("CATA_190h") == (LocalProfile.KnownLedgerCard("CATA_190h") && LocalShop.IsStandardCard(Cards.FromId("CATA_190h"))),
            "CATA_190h is usable exactly as the editor accepts it");
        Check(neutral.All(NativeDeckCodeImport.Usable) && pack.All(NativeDeckCodeImport.Usable), "Standard cards stay usable");

        // Death Knight: cards that would push a deck over three runes are dropped and counted.
        var dkClass = LocalDecks.HeroClass("HERO_11");
        var runed = Cards.All.Where(card => card.Class == dkClass && NativeDeckCodeImport.Usable(card.Id) && LocalDecks.RuneCounts(new[] { card }).Sum() > 0 && LocalDecks.RuneCounts(new[] { card }).Sum() <= 3)
            .OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
        Card runeA = null, runeB = null;
        foreach (var x in runed) { runeB = runed.FirstOrDefault(y => LocalDecks.RuneCounts(new[] { x, y }).Sum() > 3); if (runeB != null) { runeA = x; break; } }
        Check(runeA != null, "fixture: two Death Knight cards that need more than three runes together");
        var runePlan = Plan("HERO_11", Main((runeA.Id, 1), (runeB.Id, 1)));
        Check(runePlan.Cards.SequenceEqual(new[] { runeA.Id }) && runePlan.OverLimit == 1
            && LocalDecks.RuneCounts(runePlan.Cards.Select(Cards.FromId)).Sum() <= 3, "a Death Knight code is trimmed to three runes and the rest counted");


        // Refusals.
        Refuses(() => Plan("HERO_99", Main((neutral[0], 2))), DeckCodeRefusalReason.UnsupportedClass, "an unsupported class is refused");
        Refuses(() => Plan(null, Main((neutral[0], 2))), DeckCodeRefusalReason.UnsupportedClass, "an unknown hero is refused");
        Refuses(() => Plan(Mage, Main()), DeckCodeRefusalReason.Empty, "an empty code is refused");
        Refuses(() => Plan(Mage, Main((neutral[0], 0))), DeckCodeRefusalReason.Empty, "a code of zero copies is refused");
        Refuses(() => Plan(Mage, Main((neutral[0], -1))), DeckCodeRefusalReason.Malformed, "a negative count is refused");
        Refuses(() => Plan(Mage, Main((neutral[0], 100000))), DeckCodeRefusalReason.Malformed, "a huge count is refused before it expands");
        Refuses(() => Plan(Mage, Main(("NOT_A_CARD", 2), (notInPool, 2), (warriorOnly, 2))), DeckCodeRefusalReason.NoUsableCards, "a code with no usable card is refused");
        // Oversize: forty legal singles cannot fit a thirty-card deck.
        var forty = Pool.Where(card => card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL && card.Rarity != Rarity.LEGENDARY && !LocalCardCatalog.IsCore(card.Id)
            && LocalDecks.FabledBundle(card.Id).Count == 0 && !LocalDecks.IsUnsupportedSideboardOwner(card.Id) && LocalDecks.DeckSize(new[] { card.Id }) == 30)
            .Select(card => card.Id).Take(35).ToArray();
        Refuses(() => Plan(Mage, Main(forty.Select(id => (id, 1)).ToArray())), DeckCodeRefusalReason.Oversize, "more cards than a deck holds is refused");

        // Name: the code's name, cut to 32 UTF-16 code units; the usual name when it has none or none is valid.
        Check(Plan(Mage, Main((neutral[0], 2)), name: "  Face  Hunter ").Name == "Face  Hunter", "the name is trimmed");
        Check(Plan(Mage, Main((neutral[0], 2)), name: new string('x', 80)).Name == new string('x', 32), "a long name is cut to 32 characters");
        Check(Plan(Mage, Main((neutral[0], 2)), name: "a\nb\tc").Name == "abc", "control characters leave the name");
        Check(Plan(Mage, Main((neutral[0], 2)), name: null).Name == "New deck" && Plan(Mage, Main((neutral[0], 2)), name: "   ").Name == "New deck"
            && Plan(Mage, Main((neutral[0], 2)), name: "​").Name == "New deck", "no usable name falls back to the usual one");

        // A Wild code: Wild-only cards are not in the Standard pool. They are dropped: the usable set follows the profile ledger (PR #136 decides).
        var wild = Plan(Mage, Main((neutral[0], 2), (notInPool, 2)));
        Check(wild.Cards.Count == 2 && wild.Unusable == 2, "a Wild code keeps its Standard cards and drops the Wild-only ones");

        // Garbage never reaches the profile.
        Refuses(() => NativeDeckCodeImport.Plan(Mage, new string('z', 5000), "New deck", Main(("!!", 1), ("", 1)), null), DeckCodeRefusalReason.NoUsableCards, "garbage card ids are refused");

        return Profile(neutral, mageOnly, legendary, pack);
    }

    // The atomic profile create: one commit holds the draft, its cards, sideboards and name; a refused or failed create leaves nothing.
    private static int Profile(string[] neutral, string mageOnly, string legendary, string[] pack)
    {
        string root = Path.Combine(Path.GetTempPath(), "deck-code-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            long boardId; string pickId;
            using var profile = new LocalProfile(path);
            var ids = neutral.Take(15).SelectMany(id => new[] { id, id }).ToList();
            byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
            long nextId = profile.NextCustomDeckId; int count = profile.CustomDecks.Count;
            foreach (string stage in new[] { "write", "flush", "replace" })
            {
                bool reached = false;
                profile.CommitFailure = at => { if (at == stage) { reached = true; throw new IOException("injected " + stage); } };
                Rejects(() => profile.CreateDraftFromCode("Imported", Mage, ids, null), stage + " failure surfaces");
                Check(reached && profile.CustomDecks.Count == count && profile.NextCustomDeckId == nextId
                    && (File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>()).SequenceEqual(before), stage + " failure leaves no deck, no allocator step and the same bytes");
            }
            profile.CommitFailure = null;
            Rejects(() => profile.CreateDraftFromCode("Imported", Mage, ids.Take(10).Concat(new[] { "NOT_A_CARD" }), null), "an unknown card is refused by the profile");
            Rejects(() => profile.CreateDraftFromCode("Imported", "HERO_99", ids, null), "an unsupported hero is refused by the profile");
            Rejects(() => profile.CreateDraftFromCode("", Mage, ids, null), "an empty name is refused by the profile");
            Check(profile.CustomDecks.Count == count && profile.NextCustomDeckId == nextId, "refused creates leave the profile untouched");

            var ownedBefore = profile.StoredCollection.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value).ToList();
            var ownedPackBefore = pack.Select(id => profile.OwnedCount(id)).ToArray();
            string packName = "Pack " + string.Concat(Enumerable.Repeat("\U0001F600", 14));
            var packIds = pack.SelectMany(id => new[] { id, id }).ToList();
            var packDeck = profile.CreateDraftFromCode(NativeDeckCodeImport.Name(Mage, packName, "New deck"), Mage, packIds, null);
            Check(packDeck.Cards.SequenceEqual(packIds) && profile.MissingDeckSlotCount(packDeck.Id) > 0 && packDeck.Name.Length <= 32,
                "a pack-card deck with an emoji name is saved, its unowned pack cards as missing slots");
            Check(ownedPackBefore.SequenceEqual(pack.Select(id => profile.OwnedCount(id))), "importing pack cards grants none");
            var saved = profile.CreateDraftFromCode("Imported deck", Mage, ids, null);
            Check(profile.CustomDecks.Count == count + 2 && saved.Id == nextId + 1
                && saved.Name == "Imported deck" && saved.Hero == Mage && saved.Cards.SequenceEqual(ids), "one create adds exactly one deck with its cards and name");
            int missing = profile.MissingDeckSlotCount(saved.Id);
            Check(missing > 0 && !profile.IsDeckComplete(saved.Id), "cards the player does not own stay in the deck as missing slots and the deck cannot be played");
            Check(ownedBefore.SequenceEqual(profile.StoredCollection.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)),
                "import grants no card: every owned count is the same before and after");
            profile.CommitFailure = null;
            var again = profile.CreateDraftFromCode("Imported deck", Mage, ids, null);
            Check(again.Id != saved.Id && profile.CustomDecks.Count == count + 3, "pasting the same code twice makes two decks");
            // Sideboards land in the same commit and survive a restart.
            pickId = LocalDecks.CustomCards.Where(card => LocalDecks.IsCustomCard(card.Id) && LocalDecks.SideboardEligible("JAIL_397", card.Id) && card.Id != "JAIL_397"
                && !neutral.Contains(card.Id)).OrderBy(card => card.Id, StringComparer.Ordinal).First().Id;
            var boards = new List<LocalSideboard> { new LocalSideboard { Source = "JAIL_397", Cards = new List<string> { pickId } } };
            var withBoard = profile.CreateDraftFromCode("Board deck", "HERO_04", new[] { "JAIL_397" }.Concat(ids.Take(10)).ToList(), boards);
            boardId = withBoard.Id;
            Check(withBoard.Sideboards.Count == 1 && withBoard.Sideboards[0].Cards.SequenceEqual(new[] { pickId }), "an imported sideboard is saved with its deck");
            Rejects(() => profile.CreateDraftFromCode("Bad board", "HERO_04", new[] { "JAIL_397" }.Concat(ids.Take(10)).ToList(),
                new List<LocalSideboard> { new LocalSideboard { Source = "JAIL_831", Cards = new List<string> { pickId } } }), "a sideboard whose owner is absent is refused by the profile");
            // Over the copy limit across printings: refused whole.
            Rejects(() => profile.CreateDraftFromCode("Too many", Mage, new[] { legendary, legendary }, null), "a Legendary twice is refused by the profile");
            // Cap: the last slot below the cap accepts, the next is refused with the cap message and no empty draft.
            while (profile.CustomDecks.Count < LocalProfile.MaximumCustomDecks - 1) profile.CreateDraft("Capacity", Mage);
            long last = profile.NextCustomDeckId;
            var fitting = profile.CreateDraftFromCode("Last slot", Mage, ids, null);
            Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks && fitting.Id == last, "the slot below the cap takes an import");
            long atCap = profile.NextCustomDeckId;
            Rejects(() => profile.CreateDraftFromCode("Over cap", Mage, ids, null), "the cap refuses an import");
            Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks && profile.NextCustomDeckId == atCap, "the cap leaves no empty draft and no allocator step");
            Check(profile.GetDraft(boardId) != null, "board deck present before restart");
            string name = saved.Name; var savedCards = saved.Cards.ToList(); long savedId = saved.Id;
            profile.Dispose();
            using var reopened = new LocalProfile(path);
            var back = reopened.GetDraft(savedId);
            Check(back != null && back.Name == name && back.Hero == Mage && back.Cards.SequenceEqual(savedCards) && reopened.CustomDecks.Count == LocalProfile.MaximumCustomDecks,
                "the imported deck reopens identical after a restart");
            Check(reopened.GetDraft(boardId).Sideboards.Single().Source == "JAIL_397" && reopened.GetDraft(boardId).Sideboards.Single().Cards.SequenceEqual(new[] { pickId }),
                "the imported sideboard reopens identical after a restart");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine(checks + " deck-code checks passed");
        return checks;
    }
}
