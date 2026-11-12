using System.Reflection;
using System.Xml.Serialization;

internal static class ProfileCatalogChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static LocalProfileData Read(string path)
    {
        using var stream = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
    }

    internal static void Run(string testRoot)
    {
        string root = Path.Combine(testRoot, "profile-catalog"); Directory.CreateDirectory(root);
        if (Path.DirectorySeparatorChar == '/' && root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        EmptyCreate(Path.Combine(root, "empty"));
        LegacyAndSwitch(Path.Combine(root, "legacy"));
        UnsupportedSlotRecovery(Path.Combine(root, "unsupported"));
        CosmeticsOnly(Path.Combine(root, "cosmetics-only"));
        Cheats(Path.Combine(root, "cheats"));
        SeededLegendCatchUp(Path.Combine(root, "legend-catchup"));
        HistoricalCatchUp(Path.Combine(root, "historical-catchup"));
        MetadataFailures(Path.Combine(root, "failures"));
        LimitsAndFaults(Path.Combine(root, "limits"));
        WelcomeCreation(Path.Combine(root, "welcome"));
        SandboxAllDecks(Path.Combine(root, "all-decks"));
        SaveDeletion(Path.Combine(root, "delete"));
    }

    // Sandbox "All decks" (owner 2026-10-28): every offered ready-made deck once at creation, with zero-gold receipts.
    private static void SandboxAllDecks(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        void Write(string file, LocalProfileData data) { using var stream = File.Create(file); serializer.Serialize(stream, data); }
        var offered = LocalShop.Default.Products.Where(product => product.Kind == "Deck" && !product.verify && !LocalShop.Default.Withheld(product)).ToList();
        Check(offered.Count > 0 && offered.Count <= LocalProfile.MaximumCustomDecks, "offered ready-made decks fit the personal deck limit");
        var previousTop = LocalProfile.NpcLegendTopRating;
        try
        {
            LocalProfile.NpcLegendTopRating = _ => 1850;
            var cases = new (string Name, ProfileOptions Options)[] {
                ("Alone", new ProfileOptions { AllDecks = true }), ("Cards", new ProfileOptions { AllDecks = true, AllCards = true }),
                ("Cosmetics", new ProfileOptions { AllDecks = true, AllCosmetics = true }), ("Gold", new ProfileOptions { AllDecks = true, InfiniteGold = true }),
                ("Dust", new ProfileOptions { AllDecks = true, InfiniteDust = true }), ("Legend", new ProfileOptions { AllDecks = true, StartLegendOne = true }),
                ("Everything", new ProfileOptions { AllDecks = true, AllCards = true, AllCosmetics = true, InfiniteGold = true, InfiniteDust = true, StartLegendOne = true }) };
            string alonePath = null;
            foreach (var (name, options) in cases)
            {
                var slot = ProfileCatalog.Create(legacy, "Decks " + name, options);
                string path = ProfileCatalog.ProfilePathForId(legacy, slot.Id);
                var saved = Read(path);
                var savedOptions = ProfileCatalog.OptionsForProfile(legacy, path);
                Check(savedOptions.AllDecks && File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "profile-options.xml")).Contains("<AllDecks>true</AllDecks>"),
                    "All decks is stored in the save options: " + name);
                Check(saved.Purchases.Count == offered.Count && saved.PurchaseSequence == offered.Count
                    && saved.Purchases.Select(r => r.ProductId).SequenceEqual(offered.Select(product => product.Id))
                    && saved.Purchases.All(r => r.Kind == "Deck" && r.RequestId == LocalProfile.SandboxDeckRequestPrefix + r.ProductId
                        && r.PriceBeforeKit == 0 && r.GoldPaid == 0 && r.KitId == null)
                    && offered.All(product => saved.OwnedRecipes.Contains(product.RecipeId))
                    && saved.CustomDecks.Count == offered.Count && saved.CustomDecks.Select(deck => deck.SourceRecipeId).SequenceEqual(offered.Select(product => product.RecipeId)),
                    "every offered deck granted once, in catalogue order, with its recipe and receipt: " + name);
                Check(saved.GoldBalance >= LocalWelcomeGift.Gold && saved.DeckKits.Single(k => k.Id == LocalWelcomeGift.KitId).RedeemedPurchaseSequence == null,
                    "free decks spend no gold and leave the welcome kit unredeemed: " + name);
                if (options.AllCards) Check(saved.Purchases.All(r => r.Granted.Cards.Count == 0), "Full collection first: All decks grants no further copies");
                byte[] bytes = File.ReadAllBytes(path);
                using (var profile = new LocalProfile(path, savedOptions))
                {
                    Check(profile.CustomDecks.All(deck => profile.IsDeckComplete(deck.Id)), "every granted deck is complete: " + name);
                    Check(saved.Collection.All(card => card.Count <= LocalCrafting.Playset(card.Id)) && saved.CoreGrants.All(card => card.Count <= LocalCrafting.Playset(card.Id)),
                        "no Normal copy beyond a playset: " + name);
                    Check(offered.All(product => profile.HasDeckReceipt(product.RecipeId) && profile.OwnsRecipe(product.RecipeId)),
                        "the shop shows every ready-made deck as owned: " + name);
                    profile.SelectCardBack(0);
                }
                Check(File.ReadAllBytes(path).SequenceEqual(bytes), "reload and commit keep the All decks save byte-equal: " + name);
                if (name == "Alone") alonePath = path;
            }
            // Strict provenance: the receipts are valid only with the option, as the ledger's first entries, at zero gold.
            var alone = Read(alonePath!);
            var withOption = new ProfileOptions { AllDecks = true };
            string tamper = Path.Combine(root, "tamper.xml");
            void Rejected(string what, Action<LocalProfileData> change, ProfileOptions options)
            {
                var bad = LocalProfile.Clone(alone); change(bad); Write(tamper, bad);
                Reject(() => { using var profile = new LocalProfile(tamper, options); }, what);
            }
            Rejected("sandbox receipts without the All decks option", _ => { }, new ProfileOptions());
            Rejected("owned recipe without provenance", d => { d.Purchases.RemoveAt(d.Purchases.Count - 1); d.PurchaseSequence--; }, withOption);
            Rejected("sandbox receipt with a price", d => d.Purchases[0].PriceBeforeKit = 100, withOption);
            Rejected("sandbox receipt that charged gold", d => { d.Purchases[0].GoldPaid = 1; d.Purchases[0].PriceBeforeKit = 1; }, withOption);
            Rejected("sandbox receipt for another product id", d => d.Purchases[0].RequestId = LocalProfile.SandboxDeckRequestPrefix + "other", withOption);
            Rejected("free deck receipt outside Sandbox", d => d.Purchases[0].RequestId = "free-deck", withOption);
            Rejected("sandbox receipt with a kit", d => d.Purchases[0].KitId = LocalWelcomeGift.KitId, withOption);
            using (var plain = new LocalProfile(ProfileCatalog.ProfilePathForId(legacy, ProfileCatalog.Create(legacy, "Decks plain", new ProfileOptions()).Id)))
            {
                var deck = offered.First(); int price = plain.PurchasePrice(deck.Id);
                Check(price > 0 && plain.Purchases.Count == 0 && plain.CustomDecks.Count == 0, "a plain save gets no free decks");
            }
            var plainData = Read(ProfileCatalog.ProfilePathForId(legacy, ProfileCatalog.List(legacy).Single(row => row.Name == "Decks plain").Id));
            plainData.OwnedRecipes.Add(offered.First().RecipeId); Write(tamper, plainData);
            Reject(() => { using var profile = new LocalProfile(tamper, new ProfileOptions()); }, "owned recipe without any receipt");

            // Options files from before All decks (no element) still load; a plain new file writes no element.
            var plainSlot = ProfileCatalog.List(legacy).Single(row => row.Name == "Decks plain");
            string plainOptions = Path.Combine(Path.GetDirectoryName(ProfileCatalog.ProfilePathForId(legacy, plainSlot.Id))!, "profile-options.xml");
            Check(!File.ReadAllText(plainOptions).Contains("AllDecks"), "a save without All decks writes no AllDecks element (older builds keep reading it)");
            File.WriteAllText(plainOptions, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ProfileOptions xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
                + "  <Version>1</Version>\n  <MetaId>" + ProfileCatalog.MetaId + "</MetaId>\n  <AllCards>false</AllCards>\n  <AllCosmetics>true</AllCosmetics>\n"
                + "  <InfiniteGold>false</InfiniteGold>\n  <InfiniteDust>false</InfiniteDust>\n  <StartLegendOne>false</StartLegendOne>\n  <LegendRating>0</LegendRating>\n</ProfileOptions>\n");
            var old = ProfileCatalog.OptionsForProfile(legacy, ProfileCatalog.ProfilePathForId(legacy, plainSlot.Id));
            Check(old.AllCosmetics && !old.AllDecks && ProfileCatalog.List(legacy).Single(row => row.Id == plainSlot.Id).Unavailable == null,
                "an options file written before All decks loads unchanged");

            // The cap: never past the personal deck limit (a small injected limit stands in for a larger shop).
            string capped = Path.Combine(root, "capped.xml");
            using (var fresh = new LocalProfile(capped)) fresh.SelectCardBack(0);
            var cap = Read(capped);
            Check(LocalProfile.GrantAllDecks(cap, false, 3) == 3 && cap.CustomDecks.Count == 3 && cap.Purchases.Count == 3, "All decks stops at the deck limit");
            Write(capped, cap);
            using (var profile = new LocalProfile(capped, withOption))
                Check(profile.CustomDecks.Count == 3 && profile.CustomDecks.All(deck => profile.IsDeckComplete(deck.Id)), "capped grant is valid and complete");
            Reject(() => LocalProfile.GrantAllDecks(cap, false, 50), "All decks applies only to a ledger without purchases");
            Console.WriteLine("PASS Sandbox All decks: " + offered.Count + " decks once, every option combination, strict receipts, old options, deck cap");
        }
        finally { LocalProfile.NpcLegendTopRating = previousTop; }
    }

    // #57: every save made by the create-save form gets the welcome gift once, in its creation commit, cheats included.
    private static void WelcomeCreation(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var previousTop = LocalProfile.NpcLegendTopRating;
        try
        {
            LocalProfile.NpcLegendTopRating = _ => 1850;
            var cases = new (string Name, ProfileOptions Options)[] {
                ("Plain", new ProfileOptions()), ("Full collection", new ProfileOptions { AllCards = true }),
                ("All cosmetics", new ProfileOptions { AllCosmetics = true }), ("Infinite gold", new ProfileOptions { InfiniteGold = true }),
                ("Infinite dust", new ProfileOptions { InfiniteDust = true }), ("Legend one", new ProfileOptions { StartLegendOne = true }),
                ("Everything", new ProfileOptions { AllCards = true, AllCosmetics = true, InfiniteGold = true, InfiniteDust = true, StartLegendOne = true }) };
            foreach (var (name, options) in cases)
            {
                var slot = ProfileCatalog.Create(legacy, "Welcome " + name, options);
                string path = ProfileCatalog.ProfilePathForId(legacy, slot.Id);
                var saved = Read(path);
                int milestonePacks = options.StartLegendOne ? saved.MilestoneCatchUp.TierRewards.SelectMany(row => row.Grants)
                    .Where(grant => grant.Kind == LocalTierGrantKind.Pack).Sum(grant => grant.Count) : 0;
                bool plainWallet = !options.StartLegendOne && !options.AllCards;
                var kit = saved.DeckKits.SingleOrDefault(row => row.Id == LocalWelcomeGift.KitId);
                Check(LocalWelcomeGift.IsKnown(saved.Welcome) && !saved.Welcome.Seen && kit != null && kit.GoldCap == LocalWelcomeGift.KitGoldCap
                    && kit.MilestoneId == LocalWelcomeGift.KitMilestoneId && !kit.RedeemedPurchaseSequence.HasValue
                    && saved.UnopenedPacks.Single(row => row.PackType == FairPackRules.Standard).Count == LocalWelcomeGift.Packs + milestonePacks
                    && (plainWallet ? saved.GoldBalance == LocalWelcomeGift.Gold && saved.DustBalance == LocalWelcomeGift.Dust
                        : saved.GoldBalance >= LocalWelcomeGift.Gold && saved.DustBalance >= LocalWelcomeGift.Dust),
                    "create-save form grants the welcome gift once: " + name);
                Check(saved.Welcome.Cards.Count == 43 && (saved.Collection.SingleOrDefault(row => row.Id == "BE_036")?.Count ?? 0) == (LocalProfile.KnownLedgerCard("BE_036") ? 1 : 0)
                    && saved.PremiumCollection.Single(row => row.Id == "CATA_EVENT_000" && row.Premium == 1).Count == 2,
                    "create-save form durably includes the login and Golden event grants: " + name);
                if (options.StartLegendOne)
                    Check(saved.MilestoneCatchUp.Id == LocalProfile.CreationReceiptId && saved.MilestoneCatchUp.Acknowledged
                        && saved.DeckKits.Count == 2 && saved.DeckKits.Any(row => row.Id != LocalWelcomeGift.KitId),
                        "welcome gift coexists with the creation:v1 Legend receipt and its ladder kit: " + name);
                using (var profile = new LocalProfile(path, ProfileCatalog.OptionsForProfile(legacy, path)))
                {
                    Check(!profile.GrantWelcomeIfNew() && profile.WelcomePopupPending, "opening a created save never grants again: " + name);
                    Check(profile.MarkWelcomeSeen() && !profile.MarkWelcomeSeen(), "Seen commits once: " + name);
                }
                using (var profile = new LocalProfile(path, ProfileCatalog.OptionsForProfile(legacy, path)))
                    Check(!profile.WelcomePopupPending && !profile.GrantWelcomeIfNew() && Read(path).GoldBalance == saved.GoldBalance
                        && Read(path).UnopenedPacks.Sum(row => row.Count) == saved.UnopenedPacks.Sum(row => row.Count),
                        "restart after Seen: no popup, no second gift: " + name);
            }
            // A create that fails at its catalog commit leaves no listed save; the retry is one new save with one gift.
            int before = ProfileCatalog.List(legacy).Count;
            ProfileCatalog.FaultHook = phase => { if (phase == "before-replace:profiles.xml") throw new IOException("create fault"); };
            Reject(() => ProfileCatalog.Create(legacy, "Welcome retry", new ProfileOptions()), "create fault");
            ProfileCatalog.FaultHook = null;
            Check(ProfileCatalog.List(legacy).Count == before, "failed create lists no save");
            var retry = ProfileCatalog.Create(legacy, "Welcome retry", new ProfileOptions());
            var retried = Read(ProfileCatalog.ProfilePathForId(legacy, retry.Id));
            Check(ProfileCatalog.List(legacy).Count == before + 1 && retried.GoldBalance == LocalWelcomeGift.Gold
                && retried.UnopenedPacks.Single().Count == LocalWelcomeGift.Packs && retried.DeckKits.Count == 1,
                "retried create grants exactly once");
            // First launch without a catalog (fixed lane/Mac path): the open path grants once; existing files never.
            string fixedPath = Path.Combine(root, "fixed", "profile.xml"); Directory.CreateDirectory(Path.GetDirectoryName(fixedPath)!);
            using (var profile = new LocalProfile(fixedPath, new ProfileOptions()))
                Check(profile.GrantWelcomeIfNew() && !profile.GrantWelcomeIfNew() && profile.Gold == LocalWelcomeGift.Gold, "first launch grants once");
            using (var profile = new LocalProfile(fixedPath, new ProfileOptions()))
                Check(!profile.GrantWelcomeIfNew() && profile.Gold == LocalWelcomeGift.Gold && profile.UnopenedPacks == LocalWelcomeGift.Packs,
                    "second launch never re-grants");
            Console.WriteLine("PASS welcome gift: create-save form (plain and every cheat), creation:v1 coexists, Seen, restart, failed create, first launch");
        }
        finally { ProfileCatalog.FaultHook = null; LocalProfile.NpcLegendTopRating = previousTop; }
    }

    private static void SeededLegendCatchUp(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var previousTop = LocalProfile.NpcLegendTopRating;
        var previousNumber = LocalProfile.NpcLegendNumber;
        try
        {
            LocalProfile.NpcLegendTopRating = _ => 1885;
            LocalProfile.NpcLegendNumber = null;
            var slot = ProfileCatalog.Create(legacy, "Seeded Legend", new ProfileOptions { StartLegendOne = true });
            string path = ProfileCatalog.ProfilePathForId(legacy, slot.Id);
            var options = ProfileCatalog.OptionsForProfile(legacy, path);
            // Creation grants every milestone the seed already holds (floors, Legend, top 100, #1) plus the linked
            // ranked achievements, acknowledged and atomically with the new ledger.
            var created = Read(path);
            var allMilestones = MilestoneCatalog.V1.Milestones.Select(row => row.Id).ToArray();
            string[] linked = { "welcome-to-legend", "among-the-hundred", "there-can-be-only-one" };
            Check(created.MilestoneCatchUp?.Id == "creation:v1" && created.MilestoneCatchUp.Acknowledged
                && created.MilestoneCatchUp.TierRewards.Select(row => row.MilestoneId).SequenceEqual(allMilestones)
                && created.LadderState.FloorsRewarded.SequenceEqual(allMilestones) && created.LadderRuleVersion == 0
                && new[] { "Silver", "Gold", "Platinum", "Diamond", "Legend", "Elite", "Champion" }
                    .All(tier => created.OwnedCardBacks.Contains(LocalShop.LadderExclusiveCardBacks[tier]))
                && created.Trophies.Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "trophy:legend", "trophy:legend-1" })
                && created.Trophies.All(row => row.MatchId == "creation:v1") && created.LegendDeck?.Legacy == true
                && created.Achievements.Unlocks.Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal)
                    .SequenceEqual(linked.OrderBy(id => id, StringComparer.Ordinal))
                && created.Achievements.Unlocks.All(row => row.Provenance == "Creation" && row.MatchId == null)
                && created.PendingNonMatchAchievementNoticeId == null && created.UnopenedPacks.Sum(row => row.Count) > 0,
                "Legend #1 creation did not grant its milestones and linked achievements at creation");
            LocalProfile.NpcLegendNumber = (_, __) => 1;
            using (var profile = new LocalProfile(path)) {
                Check(profile.Ladder.Rating == 1885 && profile.PendingMilestoneCatchUp == null, "new seeded Legend has no retro catch-up");
                var match = profile.Begin("mage", "warrior", 17);
                var loss = profile.Settle(match.Id, "Lost", 8, false, new LocalRewardFacts(), 1800);
                profile.AcknowledgeRewardPresentation(match.Id);
                Check(loss.TierRewards.Count == 0 && loss.AchievementGrants.Count == 0 && profile.Ladder.Rating < 1885,
                    "seeded loss at Legend #1 paid tiers or ranked achievements again");
            }
            LocalProfile.NpcLegendNumber = null;
            var settled = Read(path); byte[] settledBytes = File.ReadAllBytes(path);
            Check(settled.MilestoneCatchUp.Id == "creation:v1" && settled.LadderState.FloorsRewarded.SequenceEqual(allMilestones)
                && settled.Achievements.Unlocks.Count == linked.Length && settled.OwnedCardBacks.SequenceEqual(created.OwnedCardBacks),
                "first seeded loss changed creation grants");
            var forged = Read(path); forged.MilestoneCatchUp.TierRewards.RemoveAll(row => row.MilestoneId == "legend:1");
            string forgedPath = Path.Combine(root, "creation-unlinked.xml");
            using (var stream = File.Create(forgedPath)) new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, forged);
            Reject(() => { using var invalid = new LocalProfile(forgedPath, options); }, "creation achievement without its creation milestone");
            LocalProfile.NpcLegendTopRating = _ => 1999;
            void ReopenUnchanged(string phase)
            {
                using (var profile = new LocalProfile(path))
                    Check(profile.History.Count == 1 && profile.History[0].Result == "Lost"
                        && profile.Ladder.Rating == settled.Matches[0].Ladder.RatingAfter.Value && profile.PendingMilestoneCatchUp == null,
                        phase + " reseeded rating, lost history or added retro catch-up");
                Check(File.ReadAllBytes(path).SequenceEqual(settledBytes),
                    phase + " changed saved cards, packs, gold, receipts or other settled progress");
            }
            ReopenUnchanged("first reopen"); ReopenUnchanged("second reopen");
            string backup = LocalProfileBackups.Create(path, settledBytes, "purchase", 14);
            using (var profile = new LocalProfile(path)) profile.CreateDraft("After backup", "HERO_08");
            LocalProfileBackups.Restore(path, backup, options: options);
            ReopenUnchanged("restored reopen"); ReopenUnchanged("second restored reopen");

            string corruptPath = Path.Combine(root, "invalid-receipt.xml");
            settled.Matches[0].Ladder.RatingAfter += 1;
            using (var stream = File.Create(corruptPath)) new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, settled);
            byte[] corruptBytes = File.ReadAllBytes(corruptPath);
            Reject(() => { using var invalid = new LocalProfile(corruptPath, options); }, "seeded invalid rating receipt");
            Check(File.ReadAllBytes(corruptPath).SequenceEqual(corruptBytes), "seeded invalid receipt rejection rewrote the profile");
            Console.WriteLine("PASS seeded Legend: creation grants milestones + linked achievements once; loss, reopens and backup restore preserve bytes without repay or rating reseed; invalid receipts rejected");
        }
        finally { LocalProfile.NpcLegendTopRating = previousTop; LocalProfile.NpcLegendNumber = previousNumber; }
    }

    private static void HistoricalCatchUp(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        foreach (bool cosmetics in new[] { false, true }) {
            var slot = ProfileCatalog.Create(legacy, "Historical " + cosmetics, new ProfileOptions { AllCosmetics = cosmetics });
            string path = ProfileCatalog.ProfilePathForId(legacy, slot.Id);
            var saved = Read(path);
            saved.LadderRuleVersion = 0; saved.LadderState = null;
            saved.Matches = Enumerable.Range(0, 15).Select(index => {
                string id = Guid.NewGuid().ToString(); int seed = index; LocalReward reward;
                do reward = LocalEconomy.RewardV4("Won", 10, false, LocalEconomy.RewardRollSeed(id, seed++));
                while (reward.Dust != 0);
                return new LocalMatchRecord { Id = id, Deck = "mage", DeckHero = "HERO_08", Opponent = "priest", Seed = seed - 1,
                    StartedUtc = "2026-10-24T00:00:00.0000000Z", Result = "Won", Turns = 10,
                    RewardVersion = 4, RewardFacts = new LocalRewardFacts(), Gold = GrindTiers.Standard.Gold(reward.Gold) };
            }).ToList();
            saved.GoldBalance = saved.Matches.Sum(match => match.Gold);
            using (var stream = File.Create(path)) new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, saved);
            using (var profile = new LocalProfile(path))
                Check(profile.UnopenedPacks == LocalWelcomeGift.Packs + 2 && profile.PendingMilestoneCatchUp.TierRewards.Single().MilestoneId == "floor:bronze-5",
                    "normal and cosmetics-only saves still receive earned historical catch-up (on top of the #57 welcome packs)");
            byte[] bytes = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path))
                Check(profile.UnopenedPacks == LocalWelcomeGift.Packs + 2 && profile.PendingMilestoneCatchUp != null, "historical catch-up survives reopen");
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "historical catch-up remains idempotent");
        }
        Console.WriteLine("PASS normal and cosmetics-only historical catch-up remains paid once");
    }

    private static void CosmeticsOnly(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var plainSlot = ProfileCatalog.Create(legacy, "Plain", new ProfileOptions());
        var cosmeticSlot = ProfileCatalog.Create(legacy, "Cosmetics", new ProfileOptions { AllCosmetics = true });
        string plainPath = ProfileCatalog.ProfilePathForId(legacy, plainSlot.Id);
        string path = ProfileCatalog.ProfilePathForId(legacy, cosmeticSlot.Id);
        IReadOnlyDictionary<string, int> plainNormal;
        using (var plain = new LocalProfile(plainPath)) plainNormal = plain.Collection;
        long[] draftIds; string selectedPet;
        int exclusiveBack = AchievementExclusiveCosmetics.All.First(row => row.Kind == "CardBack").CardBackId;
        var expected = LocalProfile.SupportedCheatPremium.ToDictionary(
            row => row.Premium + ":" + row.Id, row => row.Count, StringComparer.Ordinal);
        using (var profile = new LocalProfile(path))
        {
            Check(profile.Collection.OrderBy(row => row.Key, StringComparer.Ordinal)
                .SequenceEqual(plainNormal.OrderBy(row => row.Key, StringComparer.Ordinal)),
                "all-cosmetics changed Normal ownership");
            var actual = profile.OwnedCards.Where(row => row.Premium > 0).ToDictionary(
                row => row.Premium + ":" + row.Id, row => row.Count, StringComparer.Ordinal);
            Check(expected.Count > LocalProfile.SupportedCheatCards.Count && expected.Count == actual.Count
                && expected.All(row => actual.TryGetValue(row.Key, out int count) && count == row.Value),
                "all-cosmetics premium universe/floors differ from supported qualities");
            foreach (var card in LocalProfile.SupportedCheatCards)
            {
                int playset = LocalCrafting.Playset(card.Id);
                Check(profile.OwnedCount(card.Id, 1) == playset
                    && profile.OwnedCount(card.Id, 2) == (card[(SabberStoneCore.Enums.GameTag)1932] == 1 ? playset : 0)
                    && profile.OwnedCount(card.Id, 3) == (card[(SabberStoneCore.Enums.GameTag)2589] == 1 ? playset : 0),
                    "all-cosmetics admitted an unsupported card quality: " + card.Id);
            }
            Check(LocalCoreProgression.Heroes.All(hero => profile.OwnedCount(hero, 1) == 1)
                && LocalClassRewards.Portraits.All(portrait => profile.OwnsHeroSkin(portrait.Honored))
                && LocalShop.LadderExclusiveCardBacks.Values.All(profile.OwnsCardBack)
                && AchievementExclusiveCosmetics.All.All(row => row.Kind == "CardBack"
                    ? profile.OwnsCardBack(row.CardBackId) : profile.OwnsHeroSkin(row.HeroSkinId)),
                "earned portraits, ladder backs, or 17 achievement exclusives missing from all-cosmetics");
            Check(LocalShop.SupportedCheatPets.All(profile.OwnsPet)
                && LocalShop.SupportedCheatPetForms.All(profile.CanUsePetForm),
                "all-cosmetics did not expose every supported pet family and form");
            selectedPet = LocalShop.SupportedCheatPetForms.Last(); profile.SelectPet(selectedPet);
            Check(LocalShop.Default.Products.Where(product => product.verify && product.Kind == "HeroSkin")
                    .All(product => !profile.OwnsHeroSkin(product.HeroSkin))
                && !profile.OwnsHubTheme("BLACKEMPIRE"), "unverified cosmetics leaked into entitlement");
            Check(profile.OwnedCards.Where(row => row.Premium > 0 && !profile.Welcome.Cards.Any(grant =>
                    grant.CardId == row.Id && grant.Premium == row.Premium && grant.Count > 0))
                .All(row => profile.SeenCopies(row.Id, row.Premium) == int.MaxValue)
                && profile.Welcome.Cards.Where(grant => grant.Premium > 0 && grant.Count > 0)
                    .All(grant => profile.SeenCopies(grant.CardId, grant.Premium) == 0),
                "synthetic premiums start seen; actual welcome cards retain NEW markers");
            Check(NativeUIProjection.CollectionStacks(profile).Where(row => row.Premium > 0)
                    .ToDictionary(row => row.Premium + ":" + row.Id, row => row.Count, StringComparer.Ordinal)
                    .OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(row => row.Key, StringComparer.Ordinal)),
                "native collection projection differs from effective premium ownership");

            var golden = LocalProfile.SupportedCheatPremium.First(row => row.Premium == 1 && !LocalCoreProgression.Heroes.Contains(row.Id));
            var diamond = LocalProfile.SupportedCheatPremium.First(row => row.Premium == 2);
            var signature = LocalProfile.SupportedCheatPremium.First(row => row.Premium == 3);
            string HeroFor(string id) {
                var card = SabberStoneCore.Model.Cards.FromId(id);
                return card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL ? LocalDecks.PlayerHeroes[0]
                    : LocalDecks.PlayerHeroes.Single(hero => SabberStoneCore.Model.Cards.FromId(hero).Class == card.Class);
            }
            var versions = new[] { golden, diamond, signature };
            draftIds = new long[versions.Length];
            for (int i = 0; i < versions.Length; i++) {
                var version = versions[i]; var draft = profile.CreateDraft("Premium " + version.Premium, HeroFor(version.Id));
                draftIds[i] = draft.Id;
                profile.SavePremiumDraft(draft.Id, draft.Name, draft.Hero,
                    new[] { new LocalDeckCard { Id = version.Id, Premium = version.Premium } }, draft.SortOrder,
                    cosmetics: i == 0 ? new LocalDeckCosmetics { CardBack = exclusiveBack } : null);
            }
            var unsupported = LocalProfile.SupportedCheatCards.First(card => card[(SabberStoneCore.Enums.GameTag)1932] != 1);
            var unsupportedDraft = profile.CreateDraft("Unsupported", HeroFor(unsupported.Id));
            Reject(() => profile.SavePremiumDraft(unsupportedDraft.Id, unsupportedDraft.Name, unsupportedDraft.Hero,
                new[] { new LocalDeckCard { Id = unsupported.Id, Premium = 2 } }, unsupportedDraft.SortOrder),
                "unsupported Diamond deck version");
            byte[] beforeSale = File.ReadAllBytes(path);
            var sale = LocalCrafting.CommitPremiumBatch(profile, golden.Id, 1, CraftDirection.Disenchant, 1,
                profile.OwnedCount(golden.Id, 1));
            Check(!sale.Ok && File.ReadAllBytes(path).SequenceEqual(beforeSale),
                "synthetic premium copy minted dust or changed the durable ledger");
            Check(profile.OwnedAnyVersionCollection[golden.Id] >= golden.Count,
                "ready-made deck ownership ignored synthetic premium copies");
        }
        var raw = Read(path);
        var plainRaw = Read(plainPath);
        Check(raw.PremiumCollection.Select(row => (row.Id, row.Premium, row.Count, row.Acquired))
                .SequenceEqual(plainRaw.PremiumCollection.Select(row => (row.Id, row.Premium, row.Count, row.Acquired)))
            && raw.PortraitGrants.Count == 0 && raw.ClassRewardGrants.Count == 0
            && raw.OwnedCardBacks.SequenceEqual(plainRaw.OwnedCardBacks) && !raw.OwnedCardBacks.Contains(exclusiveBack)
            && raw.OwnedHeroSkins.Count == 0 && raw.Purchases.Count == 0 && raw.Matches.Count == 0
            && raw.OwnedPets.Count == 0 && raw.PetProgress.Count == 0
            && raw.Achievements.Unlocks.Count == 0 && raw.Achievements.Grants.Count == 0
            && raw.Achievements.Career.EligibleWins == 0 && raw.Achievements.Career.CreditedEnemyMinionDeaths == 0
            && raw.Achievements.Career.ClassWins.All(row => row.Wins == 0)
            && raw.CraftSequence == 0 && raw.LastCraft == null,
            "all-cosmetics forged durable ownership, pet progress, achievements, or receipts");
        byte[] beforeReopen = File.ReadAllBytes(path);
        using (var reopened = new LocalProfile(path))
        {
            Check(draftIds.Select(id => reopened.CustomDecks.Single(deck => deck.Id == id).CardEntries.Single().Premium)
                    .SequenceEqual(new[] { 1, 2, 3 })
                && reopened.CustomDecks.Single(deck => deck.Id == draftIds[0]).CardBack == exclusiveBack
                && expected.All(row => reopened.OwnedCount(row.Key.Substring(row.Key.IndexOf(':') + 1), row.Key[0] - '0') == row.Value)
                && reopened.EffectiveSelectedPet == selectedPet && reopened.PetXp(LocalShop.Default.PetFamily(selectedPet)) == 0,
                "premium entitlement, exclusive deck cosmetic, exact-version deck, or zero-XP pet preference failed reopen");
        }
        Check(File.ReadAllBytes(path).SequenceEqual(beforeReopen),
            "read-only all-cosmetics reopen mutated profile bytes");

        var overlaySlot = ProfileCatalog.Create(legacy, "Cosmetics raw overlay", new ProfileOptions { AllCosmetics = true });
        string overlayPath = ProfileCatalog.ProfilePathForId(legacy, overlaySlot.Id);
        var sellable = LocalProfile.SupportedCheatPremium.First(row => row.Premium == 1 && row.Count == 2
            && !LocalWelcomeGift.Cards.Any(grant => grant.CardId == row.Id && grant.Premium == row.Premium)
            && LocalCrafting.TryGetPrice(row.Id, row.Premium, out _, out _));
        var overlay = Read(overlayPath);
        overlay.PremiumCollection.Add(new LocalCardCount { Id = sellable.Id, Premium = sellable.Premium, Count = 1, Acquired = 1 });
        using (var stream = File.Create(overlayPath)) new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, overlay);
        using (var profile = new LocalProfile(overlayPath))
        {
            Check(profile.OwnedCount(sellable.Id, sellable.Premium) == 2 && profile.DisenchantablePremiumCount(sellable.Id, sellable.Premium) == 1,
                "effective premium floor did not max with a smaller durable stack");
            var sale = LocalCrafting.CommitPremiumBatch(profile, sellable.Id, sellable.Premium,
                CraftDirection.Disenchant, 1, 2);
            Check(sale.Ok && sale.DustDelta > 0 && profile.OwnedCount(sellable.Id, sellable.Premium) == 2
                && profile.DisenchantablePremiumCount(sellable.Id, sellable.Premium) == 0,
                "durable premium copy under entitlement floor was not sold exactly once");
        }
        using (var reopened = new LocalProfile(overlayPath))
            Check(reopened.OwnedCount(sellable.Id, sellable.Premium) == 2
                && reopened.DisenchantablePremiumCount(sellable.Id, sellable.Premium) == 0,
                "premium entitlement floor or durable sale receipt failed reopen");

        int goldenCount = LocalProfile.SupportedCheatPremium.Count(row => row.Premium == 1);
        int diamondCount = LocalProfile.SupportedCheatPremium.Count(row => row.Premium == 2);
        int signatureCount = LocalProfile.SupportedCheatPremium.Count(row => row.Premium == 3);
        Console.WriteLine($"PASS all-cosmetics effective-only: premium versions {goldenCount}/{diamondCount}/{signatureCount}, "
            + $"golden heroes {LocalCoreProgression.Heroes.Count()}, honored portraits {LocalClassRewards.Portraits.Length}, ladder backs {LocalShop.LadderExclusiveCardBacks.Count}");
    }

    private static void EmptyCreate(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        Check(ProfileCatalog.List(legacy).Count == 0 && !File.Exists(legacy) && !File.Exists(ProfileCatalog.CatalogPath(legacy)),
            "first selector boot must not create phantom profile/catalog");
        var slot = ProfileCatalog.Create(legacy, "Save 1", new ProfileOptions());
        string active = ProfileCatalog.ResolveActiveProfile(legacy);
        Check(slot.Name == "Save 1" && File.Exists(active) && !File.Exists(legacy), "first slot is staged under profiles, not root");
        Reject(() => ProfileCatalog.Create(legacy, "save 1", new ProfileOptions()), "case-insensitive duplicate name");
        Reject(() => ProfileCatalog.Create(legacy, "<b>bad</b>", new ProfileOptions()), "markup save name");
    }

    private static void LegacyAndSwitch(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        using (var profile = new LocalProfile(legacy)) profile.CreateDraft("Legacy", "HERO_08");
        byte[] before = File.ReadAllBytes(legacy);
        var listed = ProfileCatalog.List(legacy);
        Check(listed.Count == 1 && listed[0].Id == "legacy" && !File.Exists(ProfileCatalog.CatalogPath(legacy)), "legacy slot is virtual until catalog mutation");
        var added = ProfileCatalog.Create(legacy, "Challenge", new ProfileOptions());
        Check(File.ReadAllBytes(legacy).SequenceEqual(before) && ProfileCatalog.SelectedId(legacy) == "legacy", "create changed legacy or active selection");
        string oldRun = Guid.NewGuid().ToString("D"), newRun = Guid.NewGuid().ToString("D");
        string token = ProfileCatalog.RequestRelaunch(legacy, oldRun, added.Id);
        Check(ProfileCatalog.SelectedId(legacy) == "legacy", "old process committed target before handoff");
        Check(!ProfileCatalog.TryResolveRelaunch(legacy, newRun, null, null, out _, out _)
            && ProfileCatalog.SelectedId(legacy) == "legacy", "ordinary launch consumed stale switch intent");
        Reject(() => ProfileCatalog.TryResolveRelaunch(legacy, oldRun, oldRun, token, out _, out _), "same-run relaunch intent");
        Reject(() => ProfileCatalog.TryResolveRelaunch(legacy, newRun, oldRun, Guid.NewGuid().ToString("N"), out _, out _),
            "mismatched relaunch token");
        Check(ProfileCatalog.TryResolveRelaunch(legacy, newRun, oldRun, token, out string target, out string targetPath)
            && target == added.Id && File.Exists(targetPath) && ProfileCatalog.SelectedId(legacy) == "legacy",
            "successor did not resolve target without precommitting it");
        using (var validate = new LocalProfile(targetPath, ProfileCatalog.OptionsForProfile(legacy, targetPath))) { }
        ProfileCatalog.CompleteRelaunch(legacy, newRun, oldRun, token, target);
        Check(ProfileCatalog.SelectedId(legacy) == added.Id && !File.Exists(ProfileCatalog.SwitchPath(legacy)),
            "new run did not commit and consume target");
        string runB = newRun, runC = Guid.NewGuid().ToString("D");
        token = ProfileCatalog.RequestRelaunch(legacy, runB, "legacy");
        Check(ProfileCatalog.TryResolveRelaunch(legacy, runC, runB, token, out target, out targetPath) && target == "legacy",
            "second healthy switch was capped");
        using (var validate = new LocalProfile(targetPath)) { }
        ProfileCatalog.CompleteRelaunch(legacy, runC, runB, token, target);
        string runA = Guid.NewGuid().ToString("D"); token = ProfileCatalog.RequestRelaunch(legacy, runC, added.Id);
        Check(ProfileCatalog.TryResolveRelaunch(legacy, runA, runC, token, out target, out targetPath) && target == added.Id,
            "third healthy switch was capped");
        using (var validate = new LocalProfile(targetPath, ProfileCatalog.OptionsForProfile(legacy, targetPath))) { }
        ProfileCatalog.CompleteRelaunch(legacy, runA, runC, token, target);
        Check(!ProfileCatalog.TryResolveRelaunch(legacy, Guid.NewGuid().ToString("D"), null, null, out _, out _),
            "consumed intent restarted twice");
    }

    private static void UnsupportedSlotRecovery(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var unsupported = ProfileCatalog.Create(legacy, "Old V12", new ProfileOptions());
        string oldPath = ProfileCatalog.ProfilePathForId(legacy, unsupported.Id);
        var old = Read(oldPath); old.Version = 12;
        using (var stream = File.Create(oldPath)) new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, old);
        byte[] bytes = File.ReadAllBytes(oldPath);
        Check(ProfileCatalog.List(legacy).Any(slot => slot.Id == unsupported.Id), "unsupported slot disappeared from picker metadata");
        Reject(() => { using var rejected = new LocalProfile(oldPath, ProfileCatalog.OptionsForProfile(legacy, oldPath)); },
            "unsupported V12 slot");
        Check(File.ReadAllBytes(oldPath).SequenceEqual(bytes) && ProfileCatalog.SelectedId(legacy) == unsupported.Id,
            "failed V12 selection rewrote bytes or published a different selection");
        var fresh = ProfileCatalog.Create(legacy, "Fresh V14", new ProfileOptions());
        string freshPath = ProfileCatalog.ProfilePathForId(legacy, fresh.Id);
        using (var opened = new LocalProfile(freshPath))
            Check(Read(freshPath).Version == 15 && opened.Achievements.Unlocks.Count == 0,
                "picker recovery could not create a fresh V14 slot");
        Check(File.ReadAllBytes(oldPath).SequenceEqual(bytes), "fresh creation changed unsupported slot bytes");
    }

    private static void Cheats(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        LocalProfile.NpcLegendTopRating = _ => 1812.5;
        var options = new ProfileOptions { AllCards = true, AllCosmetics = true, InfiniteGold = true, InfiniteDust = true, StartLegendOne = true };
        var slot = ProfileCatalog.Create(legacy, "Cheats", options);
        string path = ProfileCatalog.ResolveActiveProfile(legacy);
        var savedOptions = ProfileCatalog.OptionsForProfile(legacy, path);
        Check(savedOptions.LegendRating == 1812.5, "Legend creation rating not frozen in sidecar");
        var created = Read(path);
        var complete = created.Achievements.Unlocks.SingleOrDefault(row => row.Id == "the-complete-works");
        // All cards owned covers the whole Standard collection (pack exclusions and hero cards included), so every
        // expansion roster is complete and The Complete Works is granted silently at creation, not on a first pack/craft.
        Check(complete != null && complete.Provenance == "Collection" && complete.MatchId == null
            && created.Achievements.Career.CompletedExpansion != null
            && created.OwnedCardBacks.Contains(LocalAchievementGrantReceipt.FromCatalog("the-complete-works").CardBackId)
            && created.PendingNonMatchAchievementNoticeId == null
            && created.Achievements.Unlocks.Count(row => row.Provenance == "Creation") == 3,
            "all-cards/Legend creation did not grant satisfied achievements silently at creation");
        LocalProfile.NpcLegendTopRating = _ => 1999;
        int back = 0, board = 0, committedGold = 0; string theme = null; LocalPackResult pendingPack = null;
        using (var profile = new LocalProfile(path))
        {
            var expectedCards = LocalCardCatalog.Supported.Concat(LocalDecks.LegalDeckbuildingCards)
                .Concat(FairPackRules.PackTypes.SelectMany(type => FairPackRules.Eligible(type, FairPackRules.RuleVersion)))
                .Where(card => card != null && card.Collectible && card.Implemented && card.Type != SabberStoneCore.Enums.CardType.HERO && LocalCardCatalog.InActiveMeta(card))
                .Select(card => card.Id).Distinct(StringComparer.Ordinal).ToArray();
            Check(expectedCards.All(id => profile.OwnedCount(id) == LocalCrafting.Playset(id)), "all-cards option missed playable Normal copies");
            var collectionCards = LocalShop.StandardCollectionCards.Where(card => card.Implemented && LocalCardCatalog.InActiveMeta(card)
                && !LocalCardCatalog.IsCore(card.Id)).Select(card => card.Id).Distinct(StringComparer.Ordinal).ToArray();
            Check(collectionCards.All(id => profile.OwnedCount(id) == LocalCrafting.Playset(id)) && profile.OwnedCount("CATA_190h") == 1
                && new[] { "EDR_000", "TIME_064", "JAIL_850", "TLC_468" }.All(id => profile.OwnedCount(id) == LocalCrafting.Playset(id))
                && LocalProfile.SupportedCheatCards.All(card => !LocalCoreProgression.Heroes.Contains(card.Id)),
                "all-cards option missed collection cards (pack exclusions or hero cards) or granted base hero portraits");
            Check(profile.OwnedCardBacks.Count >= LocalShop.SupportedCheatCardBacks.Count
                && LocalShop.SupportedCheatHeroSkins.All(profile.OwnsHeroSkin)
                && LocalShop.SupportedCheatHubThemes.All(profile.OwnsHubTheme)
                && LocalShop.SupportedCheatBoards.All(profile.OwnsBoard), "all-cosmetics entitlement incomplete");
            back = LocalShop.SupportedCheatCardBacks.First(); profile.SelectCardBack(back);
            theme = LocalShop.SupportedCheatHubThemes.First(); profile.SelectHubTheme(theme);
            board = LocalShop.SupportedCheatBoards.First(); profile.SelectBoard(board);
            var skin = LocalShop.SupportedCheatHeroSkins.First(id => LocalShop.Default.HeroSkinClass(id) != null);
            profile.SetFavoriteHeroSkin(LocalShop.Default.HeroSkinClass(skin), skin);
            Check(profile.Gold == ProfileCatalog.UnlimitedGold && profile.DustBalance == ProfileCatalog.UnlimitedDust,
                "infinite currency projection missing");
            int packPrice = profile.PurchasePrice("pack-escapefrom-violet-hold");
            profile.Purchase("pack-escapefrom-violet-hold", 1, packPrice, "infinite-gold-pack");
            // The stored balance keeps the welcome gift (#57); infinite gold only projects.
            Check(Read(path).GoldBalance == LocalWelcomeGift.Gold && profile.Gold == ProfileCatalog.UnlimitedGold, "infinite gold mutated stored balance");
            int beforeStoredGold = profile.StoredGoldBalance, beforePacks = profile.UnopenedPacks, beforeSequence = profile.PackSequence;
            int beforeCollection = profile.Collection.Values.Sum();
            // Packs can roll golden copies (premium collection); all-cards owns only Normal playsets, so a golden pull keeps
            // its own stored acquisition and does not count toward the Normal collection.
            var beforePremium = Read(path).PremiumCollection.ToDictionary(card => LocalPackCard.ReceiptKey(card.Id, card.Premium), card => card, StringComparer.Ordinal);
            int beforePremiumCopies = beforePremium.Values.Sum(card => card.Count);
            pendingPack = profile.BeginNativePack();
            int premiumPulls = pendingPack.Cards.Count(card => card.Premium != 0);
            Check(pendingPack.Cards.All(card => card.DuplicateGold == 0 && card.AcquiredBefore == (card.Premium == 0 ? LocalCrafting.Playset(card.Id)
                    : beforePremium.TryGetValue(card.Key, out var premium) ? premium.Acquired : 0))
                && profile.StoredGoldBalance == beforeStoredGold && profile.Collection.Values.Sum() == beforeCollection + 5 - premiumPulls
                && Read(path).PremiumCollection.Sum(card => card.Count) == beforePremiumCopies + premiumPulls
                && profile.Gold == ProfileCatalog.UnlimitedGold && profile.UnopenedPacks == beforePacks - 1
                && profile.PackSequence == beforeSequence + 1 && profile.PackRevealPending,
                "all-cards pending pack changed baseline copy/zero-duplicate-gold semantics or compared the projected wallet");
            committedGold = profile.StoredGoldBalance;
            Check(profile.PendingNonMatchAchievementNoticeId == null && profile.Achievements.Unlocks.Count == created.Achievements.Unlocks.Count,
                "first purchase/pack on an all-cards save popped or granted a collection achievement");
        }
        using (var profile = new LocalProfile(path))
        {
            var resumedPack = profile.BeginNativePack(pendingPack.PackType);
            Check(profile.StoredGoldBalance == committedGold && resumedPack.Cards.Select(card => card.Id).SequenceEqual(pendingPack.Cards.Select(card => card.Id))
                && resumedPack.Cards.All(card => card.DuplicateGold == 0),
                "baseline duplicate pending receipt failed reopen or changed durable economy");
            profile.CompleteNativePackReveal(profile.PackSequence, pendingPack.Cards.Select(card => card.Key));
            Check(profile.Ladder.StarIndex == LadderRules.LegendIndex && profile.Ladder.Rating == 1812.5, "Legend #1 seed changed after callback change");
            var pending = profile.Begin("mage", "warrior", 7);
            profile.Settle(pending.Id, "Won", 8, false, new LocalRewardFacts(), 1800);
            profile.AcknowledgeRewardPresentation(pending.Id);
        }
        LocalProfile.NpcLegendTopRating = null;
        using (var reopened = new LocalProfile(path))
            Check(reopened.Ladder.StarIndex == LadderRules.LegendIndex && reopened.History.Count == 1
                && reopened.FavoriteCardBacks.Single() != 0 && reopened.SelectedHubTheme != LocalHubCosmetics.ClassicTheme && reopened.SelectedBoard != 0,
                "cheat profile failed durable reopen");
        string backup = LocalProfileBackups.Create(path, File.ReadAllBytes(path), "purchase", 14);
        using (var changed = new LocalProfile(path))
            changed.SelectBoard(LocalShop.SupportedCheatBoards.First(value => value != changed.SelectedBoard));
        LocalProfileBackups.Restore(path, backup, options: savedOptions);
        using (var restored = new LocalProfile(path))
            Check(restored.History.Count == 1 && restored.FavoriteCardBacks.Single() == back && restored.SelectedBoard == board,
                "cheat options were not applied to staged backup validation");
        string requested = LocalProfileBackups.Create(path, File.ReadAllBytes(path), "purchase", 14);
        using (var changed = new LocalProfile(path))
            changed.SelectHubTheme(LocalShop.SupportedCheatHubThemes.First(value => value != changed.SelectedHubTheme));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "restore-request.txt"), requested + "\n");
        LocalProfileBackups.ProcessRestoreRequest(path, savedOptions);
        Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "restore-result.txt")).StartsWith("OK Restored", StringComparison.Ordinal),
            "cheat restore request failed");
        using (var restored = new LocalProfile(path))
            Check(restored.History.Count == 1 && restored.SelectedHubTheme == theme, "cheat restore request did not restore target");

        string dustRoot = Path.Combine(root, "dust"), dustLegacy = Path.Combine(dustRoot, "profile.xml"); Directory.CreateDirectory(dustRoot);
        var dustSlot = ProfileCatalog.Create(dustLegacy, "Dust", new ProfileOptions { InfiniteDust = true });
        string dustPath = ProfileCatalog.ProfilePathForId(dustLegacy, dustSlot.Id);
        using (var dust = new LocalProfile(dustPath))
        {
            var crafted = new LocalCraftingLedger(dust).CommitBatch("CATA_452", CraftDirection.Craft, 1, 0);
            Check(crafted.Ok && crafted.DustDelta == -40 && dust.DustBalance == ProfileCatalog.UnlimitedDust,
                "infinite dust did not retain nominal craft quote");
            Check(Read(dustPath).DustBalance == LocalWelcomeGift.Dust && Read(dustPath).LastCraft.DustDelta == -40,
                "infinite dust changed wallet or lost nominal receipt delta");
        }
        Console.WriteLine($"PASS profile saves: cards, cosmetics {LocalShop.SupportedCheatCardBacks.Count}/"
            + $"{LocalShop.SupportedCheatHeroSkins.Count}/{LocalShop.SupportedCheatBoards.Count}/"
            + $"{LocalShop.SupportedCheatHubThemes.Count}, infinite wallets, Legend and backup reopen");
    }

    private static void MetadataFailures(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        ProfileCatalog.Create(legacy, "Good", new ProfileOptions());
        string catalog = ProfileCatalog.CatalogPath(legacy);
        string xml = File.ReadAllText(catalog).Replace("</ProfileCatalogData>", "<Unknown>1</Unknown></ProfileCatalogData>");
        File.WriteAllText(catalog, xml);
        Reject(() => ProfileCatalog.List(legacy), "unknown catalog field");
    }

    private static void LimitsAndFaults(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        for (int i = 1; i <= ProfileCatalog.MaximumSlots; i++) ProfileCatalog.Create(legacy, "Save " + i, new ProfileOptions());
        Reject(() => ProfileCatalog.Create(legacy, "Save 21", new ProfileOptions()), "21st save");
        Check(ProfileCatalog.List(legacy).Count == 20, "20-save bound changed catalog");

        string selected = ProfileCatalog.SelectedId(legacy), other = ProfileCatalog.List(legacy).First(row => row.Id != selected).Id;
        ProfileCatalog.FaultHook = phase => { if (phase == "before-replace:profiles.xml") throw new IOException("catalog fault"); };
        Reject(() => ProfileCatalog.CommitSelection(legacy, other), "selection catalog replacement fault");
        ProfileCatalog.FaultHook = null;
        Check(ProfileCatalog.SelectedId(legacy) == selected && Directory.GetFiles(root, "profiles.xml.tmp.failed-*").Length == 1,
            "failed selection changed marker or discarded transaction evidence");
        ProfileCatalog.CommitSelection(legacy, other);
        Check(ProfileCatalog.SelectedId(legacy) == other, "selection retry failed");

        string missingRoot = Path.Combine(root, "missing"); Directory.CreateDirectory(missingRoot);
        string missingLegacy = Path.Combine(missingRoot, "profile.xml");
        var missing = ProfileCatalog.Create(missingLegacy, "Missing", new ProfileOptions());
        var healthy = ProfileCatalog.Create(missingLegacy, "Healthy", new ProfileOptions());
        string missingPath = ProfileCatalog.ProfilePathForId(missingLegacy, missing.Id);
        File.Delete(missingPath);
        // A damaged selected slot resolves (the selector launch always opens the picker) but cannot be opened.
        Check(ProfileCatalog.ResolveActiveProfile(missingLegacy) == missingPath, "missing selected slot blocked catalog resolution");
        var listedMissing = ProfileCatalog.List(missingLegacy);
        Check(listedMissing.Count == 2 && listedMissing.Single(row => row.Id == missing.Id).Unavailable == "save file missing"
            && listedMissing.Single(row => row.Id == healthy.Id).Unavailable == null, "damaged slot not isolated in picker listing");
        Reject(() => ProfileCatalog.ProfilePathForId(missingLegacy, missing.Id), "opened missing selected file");
        Reject(() => ProfileCatalog.CommitSelection(missingLegacy, missing.Id), "selected missing file");
        Reject(() => ProfileCatalog.RequestRelaunch(missingLegacy, Guid.NewGuid().ToString("D"), missing.Id), "relaunch to missing file");
        ProfileCatalog.CommitSelection(missingLegacy, healthy.Id);
        Check(ProfileCatalog.SelectedId(missingLegacy) == healthy.Id, "healthy save unusable beside a damaged slot");
        var extra = ProfileCatalog.Create(missingLegacy, "After damage", new ProfileOptions());
        Check(ProfileCatalog.List(missingLegacy).Count == 3 && extra.Unavailable == null, "create blocked by a damaged slot");

        string corruptRoot = Path.Combine(root, "options"); Directory.CreateDirectory(corruptRoot);
        string corruptLegacy = Path.Combine(corruptRoot, "profile.xml");
        var corrupt = ProfileCatalog.Create(corruptLegacy, "Corrupt", new ProfileOptions());
        var good = ProfileCatalog.Create(corruptLegacy, "Good", new ProfileOptions());
        string corruptPath = ProfileCatalog.ProfilePathForId(corruptLegacy, corrupt.Id);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(corruptPath)!, "profile-options.xml"), "<Unknown/>");
        Check(ProfileCatalog.List(corruptLegacy).Single(row => row.Id == corrupt.Id).Unavailable == "options unreadable",
            "corrupt immutable options not reported");
        Reject(() => ProfileCatalog.ProfilePathForId(corruptLegacy, corrupt.Id), "corrupt immutable options");
        Reject(() => ProfileCatalog.OptionsForProfile(corruptLegacy, corruptPath), "corrupt immutable options read");
        File.Delete(Path.Combine(Path.GetDirectoryName(corruptPath)!, "profile-options.xml"));
        Check(ProfileCatalog.List(corruptLegacy).Single(row => row.Id == corrupt.Id).Unavailable == "options missing", "missing options not reported");
        ProfileCatalog.CommitSelection(corruptLegacy, good.Id);

        // A relaunch target damaged after the old process wrote its intent falls back to the picker.
        string oldRun = Guid.NewGuid().ToString("D"), newRun = Guid.NewGuid().ToString("D");
        var target = ProfileCatalog.Create(corruptLegacy, "Target", new ProfileOptions());
        string token = ProfileCatalog.RequestRelaunch(corruptLegacy, oldRun, target.Id);
        File.Delete(ProfileCatalog.ProfilePathForId(corruptLegacy, target.Id));
        Check(!ProfileCatalog.TryResolveRelaunch(corruptLegacy, newRun, oldRun, token, out string targetId, out string targetPath)
            && targetId == null && targetPath == null && ProfileCatalog.SelectedId(corruptLegacy) == good.Id,
            "damaged relaunch target did not fall back to the picker");

        StaleTemporaries(Path.Combine(root, "stale"));
        ReplaceCrashWindow(Path.Combine(root, "replace-crash"));
        FailedCreateKeepsOptions(Path.Combine(root, "failed-create"));
    }

    private static void StaleTemporaries(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var first = ProfileCatalog.Create(legacy, "First", new ProfileOptions());
        var second = ProfileCatalog.Create(legacy, "Second", new ProfileOptions());
        string catalog = ProfileCatalog.CatalogPath(legacy), committed = File.ReadAllText(catalog);
        // Hard kill between WriteNew(tmp) and File.Replace: the intact catalog is still authoritative.
        File.WriteAllText(catalog + ".tmp", "<ProfileCatalogData><Partial");
        Check(ProfileCatalog.SelectedId(legacy) == first.Id && File.ReadAllText(catalog) == committed, "stale temp broke catalog reads");
        ProfileCatalog.CommitSelection(legacy, second.Id);
        Check(ProfileCatalog.SelectedId(legacy) == second.Id && !File.Exists(catalog + ".tmp")
            && Directory.GetFiles(root, "profiles.xml.tmp.stale-*").Length == 1
            && File.ReadAllText(Directory.GetFiles(root, "profiles.xml.tmp.stale-*")[0]) == "<ProfileCatalogData><Partial",
            "stale catalog temp blocked selection or lost its evidence");
        File.WriteAllText(catalog + ".tmp", "partial");
        var third = ProfileCatalog.Create(legacy, "Third", new ProfileOptions());
        Check(ProfileCatalog.List(legacy).Any(row => row.Id == third.Id) && Directory.GetFiles(root, "profiles.xml.tmp.stale-*").Length == 2,
            "stale catalog temp blocked creation");
        string run = Guid.NewGuid().ToString("D"), next = Guid.NewGuid().ToString("D");
        File.WriteAllText(ProfileCatalog.SwitchPath(legacy) + ".tmp", "partial");
        string token = ProfileCatalog.RequestRelaunch(legacy, run, first.Id);
        Check(ProfileCatalog.TryResolveRelaunch(legacy, next, run, token, out string target, out _) && target == first.Id
            && Directory.GetFiles(root, "profile-switch.xml.tmp.stale-*").Length == 1, "stale switch temp blocked in-game switching");
    }

    // Win32/Wine File.Replace: profiles.xml -> .bak, then .tmp -> profiles.xml. A crash between leaves no catalog,
    // the previous .bak and a complete .tmp. Recovery must keep every slot and the newest selection, never reset.
    private static void ReplaceCrashWindow(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = ProfileCatalog.Create(legacy, "Alpha", new ProfileOptions());
        var b = ProfileCatalog.Create(legacy, "Bravo", new ProfileOptions());
        var c = ProfileCatalog.Create(legacy, "Charlie", new ProfileOptions());
        string catalog = ProfileCatalog.CatalogPath(legacy);
        ProfileCatalog.CommitSelection(legacy, b.Id); string older = File.ReadAllText(catalog);
        ProfileCatalog.CommitSelection(legacy, c.Id); string newer = File.ReadAllText(catalog);
        var ids = new[] { a.Id, b.Id, c.Id };
        bool AllSlots() => ProfileCatalog.List(legacy).Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(ids.OrderBy(id => id, StringComparer.Ordinal));
        void Crash(string tmp, string bak)
        {
            File.Delete(catalog); File.Delete(catalog + ".bak"); File.Delete(catalog + ".tmp");
            foreach (string stale in Directory.GetFiles(root, "profiles.xml.tmp.stale-*")) File.Delete(stale);
            if (tmp != null) File.WriteAllText(catalog + ".tmp", tmp);
            if (bak != null) File.WriteAllText(catalog + ".bak", bak);
        }

        Crash(newer, older);
        Check(ProfileCatalog.SelectedId(legacy) == c.Id && AllSlots() && File.ReadAllText(catalog) == newer
            && !File.Exists(catalog + ".tmp") && File.ReadAllText(catalog + ".bak") == older,
            "mid-replace crash did not promote the complete temp with every slot and the newest selection");
        ProfileCatalog.CommitSelection(legacy, a.Id);
        Check(ProfileCatalog.SelectedId(legacy) == a.Id && AllSlots(), "recovered catalog is not writable");

        Crash("<ProfileCatalogData><Partial", older);
        Check(ProfileCatalog.SelectedId(legacy) == b.Id && AllSlots() && File.ReadAllText(catalog) == older,
            "partial temp did not fall back to the previous catalog");
        ProfileCatalog.CommitSelection(legacy, c.Id);
        Check(ProfileCatalog.SelectedId(legacy) == c.Id && Directory.GetFiles(root, "profiles.xml.tmp.stale-*").Length == 1,
            "partial temp beside a recovered catalog was not set aside as evidence");

        Crash(null, older);
        Check(ProfileCatalog.SelectedId(legacy) == b.Id && AllSlots(), "lone backup did not recover the catalog");

        Crash(newer, null);
        Check(ProfileCatalog.SelectedId(legacy) == c.Id && AllSlots(), "first-write temp did not recover the catalog");

        Crash("<broken", "<broken");
        Reject(() => ProfileCatalog.List(legacy), "unreadable recovery copies were treated as a fresh catalog");
        Reject(() => ProfileCatalog.Create(legacy, "Delta", new ProfileOptions()), "create reset a catalog whose recovery copies are unreadable");
        Check(!File.Exists(catalog) && File.ReadAllText(catalog + ".tmp") == "<broken" && File.ReadAllText(catalog + ".bak") == "<broken",
            "failed recovery changed the crash evidence");
        Crash(newer, older);
        Check(AllSlots(), "crash fixture cleanup failed");

        var copy = new ProfileOptions { AllCards = true, AllCosmetics = true, InfiniteGold = true, InfiniteDust = true, StartLegendOne = true, LegendRating = 12.5 };
        var clone = (ProfileOptions)typeof(ProfileOptions).GetMethod("Clone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(copy, null)!;
        Check(!ReferenceEquals(copy, clone) && typeof(ProfileOptions).GetFields().All(field => Equals(field.GetValue(copy), field.GetValue(clone))),
            "profile options copy dropped a field");
    }

    private static void FailedCreateKeepsOptions(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        ProfileCatalog.Create(legacy, "Taken", new ProfileOptions());
        var previous = LocalProfile.NpcLegendTopRating;
        try
        {
            LocalProfile.NpcLegendTopRating = _ => 1850;
            var wizard = new ProfileOptions { StartLegendOne = true };
            Reject(() => ProfileCatalog.Create(legacy, "taken", wizard), "duplicate name");
            Check(wizard.LegendRating == 0, "failed create froze a Legend rating into the wizard options");
            wizard.StartLegendOne = false;
            var slot = ProfileCatalog.Create(legacy, "Retry", wizard);
            Check(ProfileCatalog.OptionsForProfile(legacy, ProfileCatalog.ProfilePathForId(legacy, slot.Id)).LegendRating == 0,
                "retry after toggling Legend off failed");
        }
        finally { LocalProfile.NpcLegendTopRating = previous; }
    }
    // ---- Save deletion (startup picker): Describe, Delete, Reconcile ------------------------------------------------
    private static ProfileSlot Add(string legacy, string name, ProfileOptions options = null) => ProfileCatalog.Create(legacy, name, options ?? new ProfileOptions());
    private static string SlotDir(string legacy, string id) => Path.Combine(Path.GetDirectoryName(legacy)!, "profiles", id);
    private static string DeletedDir(string legacy) => Path.Combine(Path.GetDirectoryName(legacy)!, "profiles", ".deleted");
    private static string[] DeletedNames(string legacy) => Directory.Exists(DeletedDir(legacy))
        ? Directory.GetDirectories(DeletedDir(legacy)).Select(dir => Path.GetFileName(dir)).OrderBy(name => name, StringComparer.Ordinal).ToArray() : new string[0];
    private static string Tree(string root) => string.Join("\n", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + (Directory.Exists(path) ? "/"
            : ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) + ":" + File.GetLastWriteTimeUtc(path).Ticks)));
    // A save held open locks its .lock file against reading; everything else in the folder is compared.
    private static string HeldTree(string root) => string.Join("\n", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith(".lock", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + (Directory.Exists(path) ? "/"
            : ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) + ":" + File.GetLastWriteTimeUtc(path).Ticks)));
    private static void WriteProfile(string path, LocalProfileData data) { using var stream = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data); }
    private static string Stamp(int day) => new DateTime(2026, 1, 1).AddDays(day).ToString("yyyyMMdd") + "T000000000Z";

    private static void SaveDeletion(string root)
    {
        DeleteBasics(Path.Combine(root, "basics"));
        DeleteSelected(Path.Combine(root, "selected"));
        DeleteRefusals(Path.Combine(root, "refusals"));
        DeleteCrashes(Path.Combine(root, "crashes"));
        DeleteWhileBound(Path.Combine(root, "bound"));
        DeleteWhileBoundToRoot(Path.Combine(root, "bound-root"));
        PickerDeleteGate(Path.Combine(root, "picker-gate"));
        // These two read private lab scripts and notes; a checkout without them skips them.
        if (HasRepoFile("windows/Runtime-Manifest.Common.ps1") && HasRepoFile("tools/remote/deck/deck-remote.sh")) OptionsFieldsMatchTheScripts(Path.Combine(root, "script-fields"));
        else Console.WriteLine("SKIP script option-field validators: maintainer-only (windows/ and tools/remote/ are not in this checkout)");
        if (HasRepoFile("docs/agent-workflows/live-test-hooks.md")) PaceHoursMatchTheRecipes();
        else Console.WriteLine("SKIP pace hours against the live-test recipes: maintainer-only (docs/agent-workflows is not in this checkout)");
        ReconcileBothWays(Path.Combine(root, "reconcile"));
        ReconcileRefusesToGuess(Path.Combine(root, "reconcile-safe"));
        PurgeOldest(Path.Combine(root, "purge"));
        DescribeReadOnly(Path.Combine(root, "describe"));
        LostCatalogNeverDestroys(Path.Combine(root, "lost-catalog"));
        PendingNeverFailsOpen(Path.Combine(root, "pending-unknown"));
        ReconcileNeedsAListing(Path.Combine(root, "reconcile-listing"));
        PurgeSparesWhatItJustMoved(Path.Combine(root, "purge-clock"));
        Console.WriteLine("PASS save deletion: delete/move/log, selection clearing, refusals, crash windows, reconcile both ways (orphans kept), purge by name, read-only describe, pending never fails open");
    }

    private static void DeleteBasics(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B"); var c = Add(legacy, "Save C");
        var log = new List<string>();
        var result = ProfileCatalog.Delete(legacy, b.Id, log.Add);
        string entry = DeletedNames(legacy).Single();
        Check(result.Outcome == "deleted" && result.SlotRemoved && result.Moved == "profiles/.deleted/" + entry
            && System.Text.RegularExpressions.Regex.IsMatch(entry, "^" + b.Id + @"-\d{8}T\d{9}Z$")
            && ProfileCatalog.DeletedFolders(legacy).SequenceEqual(new[] { entry }), "delete moved the folder to .deleted/<id>-<stamp>");
        Check(log.Count == 1 && log[0] == "PROFILE_DELETE id=" + b.Id + " name=Save B moved=profiles/.deleted/" + entry + " outcome=deleted",
            "delete logs exactly one PROFILE_DELETE line: " + string.Join("|", log));
        Check(!Directory.Exists(SlotDir(legacy, b.Id)) && File.Exists(Path.Combine(DeletedDir(legacy), entry, "profile.xml"))
            && File.Exists(Path.Combine(DeletedDir(legacy), entry, "profile-options.xml"))
            && ProfileCatalog.List(legacy).Select(row => row.Id).SequenceEqual(new[] { a.Id, c.Id }), "deleted save left the catalog and kept its files");
        Check(ProfileCatalog.SelectedId(legacy) == a.Id, "deleting a non-selected save keeps the selection");
    }

    private static void DeleteSelected(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B"); var c = Add(legacy, "Save C");
        var log = new List<string>();
        Check(ProfileCatalog.Delete(legacy, a.Id, log.Add).Outcome == "deleted", "delete the selected save");
        Check(ProfileCatalog.SelectedId(legacy) == null && !File.ReadAllText(ProfileCatalog.CatalogPath(legacy)).Contains("<SelectedId")
            && ProfileCatalog.ResolveActiveProfile(legacy) == Path.GetFullPath(legacy),
            "selection is cleared and the launch resolves to the unbound root path, not a throw");
        Check(ProfileCatalog.List(legacy).Select(row => row.Id).SequenceEqual(new[] { b.Id, c.Id }), "list reads a catalog with slots and no selection");
        ProfileCatalog.CommitSelection(legacy, c.Id);
        Check(ProfileCatalog.SelectedId(legacy) == c.Id && ProfileCatalog.ResolveActiveProfile(legacy) == ProfileCatalog.ProfilePathForId(legacy, c.Id), "commit selection after a cleared one");
        Check(ProfileCatalog.Delete(legacy, c.Id, log.Add).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == null, "clear again");
        var d = Add(legacy, "Save D");
        Check(ProfileCatalog.List(legacy).Count == 2 && ProfileCatalog.SelectedId(legacy) == d.Id, "create after a cleared selection");
        // A named selection must still name a slot.
        string catalog = ProfileCatalog.CatalogPath(legacy), text = File.ReadAllText(catalog);
        File.WriteAllText(catalog, text.Replace("<SelectedId>" + d.Id + "</SelectedId>", "<SelectedId>" + new string('f', 32) + "</SelectedId>"));
        Reject(() => ProfileCatalog.List(legacy), "selection naming no slot");
        File.WriteAllText(catalog, text);

        // Last slot: an empty, valid catalog; creating works.
        string solo = Path.Combine(root, "solo"); Directory.CreateDirectory(solo); string soloLegacy = Path.Combine(solo, "profile.xml");
        var only = Add(soloLegacy, "Only");
        Check(ProfileCatalog.Delete(soloLegacy, only.Id, log.Add).Outcome == "deleted" && ProfileCatalog.List(soloLegacy).Count == 0
            && ProfileCatalog.SelectedId(soloLegacy) == null && ProfileCatalog.ResolveActiveProfile(soloLegacy) == Path.GetFullPath(soloLegacy), "last slot leaves an empty valid catalog");
        var again = Add(soloLegacy, "Again");
        Check(ProfileCatalog.List(soloLegacy).Single().Id == again.Id && ProfileCatalog.SelectedId(soloLegacy) == again.Id, "create after deleting the last slot");
    }

    private static void DeleteRefusals(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        using (var profile = new LocalProfile(legacy)) profile.CreateDraft("Legacy", "HERO_08");
        var x = Add(legacy, "Save X"); var y = Add(legacy, "Save Y");
        var log = new List<string>();
        string pendingPath = ProfileCatalog.ProfilePathForId(legacy, y.Id);
        var data = Read(pendingPath); data.Pending = new LocalMatchRecord { Id = "pending-1" }; WriteProfile(pendingPath, data);
        string before = Tree(root);
        var pending = ProfileCatalog.Delete(legacy, y.Id, log.Add);
        Check(pending.Outcome == "refused-pending" && !pending.SlotRemoved && Tree(root) == before
            && log[^1] == "PROFILE_DELETE id=" + y.Id + " name=Save Y moved=none outcome=refused-pending", "a save with a pending match is refused, byte-for-byte untouched");
        var legacyRefusal = ProfileCatalog.Delete(legacy, "legacy", log.Add);
        Check(legacyRefusal.Outcome == "refused-legacy" && Tree(root) == before && ProfileCatalog.List(legacy).Any(row => row.Id == "legacy")
            && log[^1] == "PROFILE_DELETE id=legacy name=Save 1 moved=none outcome=refused-legacy", "the root (legacy) save is never deleted");
        Check(!ProfileCatalog.Describe(legacy, "legacy").Readable, "the root save is not read for a summary");
        foreach (string unknown in new[] { new string('a', 32), "../x", null })
        {
            var refused = ProfileCatalog.Delete(legacy, unknown, log.Add);
            Check(refused.Outcome == "refused-unknown" && Tree(root) == before && !log[^1].Contains("\n")
                && log[^1] == "PROFILE_DELETE id=" + (unknown == new string('a', 32) ? unknown : "invalid") + " name=none moved=none outcome=refused-unknown",
                "unknown id refused: " + unknown);
        }
        // Fault before the catalog commit: nothing moved, nothing changed except the failed temp evidence.
        string catalogBytes = Convert.ToHexString(File.ReadAllBytes(ProfileCatalog.CatalogPath(legacy)));
        ProfileCatalog.FaultHook = phase => { if (phase == "before-replace:profiles.xml") throw new IOException("catalog fault"); };
        try
        {
            var failed = ProfileCatalog.Delete(legacy, x.Id, log.Add);
            Check(failed.Outcome.StartsWith("failed:") && !failed.SlotRemoved && Directory.Exists(SlotDir(legacy, x.Id)) && !Directory.Exists(DeletedDir(legacy))
                && Convert.ToHexString(File.ReadAllBytes(ProfileCatalog.CatalogPath(legacy))) == catalogBytes
                && log[^1].StartsWith("PROFILE_DELETE id=" + x.Id + " name=Save X moved=none outcome=failed:"), "a crash before the commit leaves everything untouched");
        }
        finally { ProfileCatalog.FaultHook = null; }
        // A save another process holds open keeps its folder.
        using (var held = new LocalProfile(ProfileCatalog.ProfilePathForId(legacy, x.Id), ProfileCatalog.OptionsForProfile(legacy, ProfileCatalog.ProfilePathForId(legacy, x.Id))))
        {
            var busy = ProfileCatalog.Delete(legacy, x.Id, log.Add);
            Check(busy.Outcome == "refused-in-use" && ProfileCatalog.List(legacy).Any(row => row.Id == x.Id) && Directory.Exists(SlotDir(legacy, x.Id)), "a save held open is refused");
        }
        Check(ProfileCatalog.Delete(legacy, x.Id, log.Add).Outcome == "deleted", "released save deletes");
    }

    // In-game Manage: everything but the open save stays deletable under the ordinary checks; the open save and its selection never move.
    private static void DeleteWhileBound(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B"); var c = Add(legacy, "Save C"); var d = Add(legacy, "Save D");
        string openPath = ProfileCatalog.ProfilePathForId(legacy, a.Id), bPath = ProfileCatalog.ProfilePathForId(legacy, b.Id);
        Check(ProfileCatalog.SelectedId(legacy) == a.Id, "first save is selected");
        var log = new List<string>();
        // Refused on identity alone, with the file lock free: refused-in-use cannot be what stops it.
        string before = Tree(root);
        var current = ProfileCatalog.Delete(legacy, a.Id, log.Add, openPath);
        Check(current.Outcome == "refused-current" && !current.SlotRemoved && Tree(root) == before
            && log[^1] == "PROFILE_DELETE id=" + a.Id + " name=Save A moved=none outcome=refused-current", "the open save is refused in the delete path, byte-for-byte untouched");
        string spelled = Path.Combine(Path.GetDirectoryName(openPath)!, "..", a.Id, "profile.xml");
        Check(ProfileCatalog.Delete(legacy, a.Id, log.Add, spelled).Outcome == "refused-current" && Tree(root) == before, "the open save is recognized by resolved path");
        // Open and selected disagree only if the catalog changed from outside: both stay refused, nothing else moves.
        var selectedOnly = ProfileCatalog.Delete(legacy, a.Id, log.Add, bPath); var pathOnly = ProfileCatalog.Delete(legacy, b.Id, log.Add, bPath);
        Check(selectedOnly.Outcome == "refused-current" && pathOnly.Outcome == "refused-current" && Tree(root) == before, "a selected-but-not-open save and an open-but-not-selected save are both refused");
        Check(ProfileCatalog.IsOpenSave(legacy, a.Id, a.Id, openPath) && ProfileCatalog.IsOpenSave(legacy, b.Id, a.Id, bPath) && ProfileCatalog.IsOpenSave(legacy, a.Id, a.Id, bPath)
            && !ProfileCatalog.IsOpenSave(legacy, b.Id, a.Id, openPath) && !ProfileCatalog.IsOpenSave(legacy, "../x", a.Id, openPath) && !ProfileCatalog.IsOpenSave(legacy, null, a.Id, openPath),
            "IsOpenSave reads the bound path and the selection, and only those");
        Check(!ProfileCatalog.IsOpenSave(legacy, a.Id, a.Id, null), "nothing is open at the unbound picker, selected or not");
        Check(ProfileCatalog.DeleteStateBlock(true, false, false) == null && ProfileCatalog.DeleteStateBlock(true, false, true) == null
            && ProfileCatalog.DeleteStateBlock(false, false, false) == "refused-bound" && ProfileCatalog.DeleteStateBlock(false, true, false) == null
            && ProfileCatalog.DeleteStateBlock(true, true, false) == null && ProfileCatalog.DeleteStateBlock(false, true, true) == "refused-match",
            "state rule: startup picker as before, unbound elsewhere closed, bound outside a match open, bound in a match closed");
        // The real bound state: the live LocalProfile holds its lock and keeps committing.
        var pendingData = Read(ProfileCatalog.ProfilePathForId(legacy, c.Id)); pendingData.Pending = new LocalMatchRecord { Id = "pending-1" };
        WriteProfile(ProfileCatalog.ProfilePathForId(legacy, c.Id), pendingData);
        string deleted = DeletedDir(legacy); Directory.CreateDirectory(deleted);
        for (int i = 0; i < 6; i++) Directory.CreateDirectory(Path.Combine(deleted, new string((char)('0' + i), 32) + "-" + Stamp(i)));
        using (var open = new LocalProfile(openPath, ProfileCatalog.OptionsForProfile(legacy, openPath)))
        {
            string openTree = HeldTree(SlotDir(legacy, a.Id)), rootBytes = File.Exists(legacy) ? Convert.ToHexString(File.ReadAllBytes(legacy)) : null;
            string catalogBefore = File.ReadAllText(ProfileCatalog.CatalogPath(legacy));
            Check(ProfileCatalog.Delete(legacy, a.Id, log.Add, open.FilePath).Outcome == "refused-current" && File.ReadAllText(ProfileCatalog.CatalogPath(legacy)) == catalogBefore, "the bound profile's own path refuses its save");
            Check(ProfileCatalog.Delete(legacy, c.Id, log.Add, open.FilePath).Outcome == "refused-pending" && Directory.Exists(SlotDir(legacy, c.Id)), "another save's pending match still refuses");
            var other = ProfileCatalog.Delete(legacy, b.Id, log.Add, open.FilePath);
            Check(other.Outcome == "deleted" && other.SlotRemoved && !Directory.Exists(SlotDir(legacy, b.Id)) && DeletedNames(legacy).Any(name => name.StartsWith(b.Id + "-"))
                && log.Any(line => line.StartsWith("PROFILE_PURGE removed=2")) && DeletedNames(legacy).Count(name => !name.EndsWith("-orphan")) == 5,
                "another save soft-deletes while bound and the purge keeps the last five");
            Check(ProfileCatalog.SelectedId(legacy) == a.Id && HeldTree(SlotDir(legacy, a.Id)) == openTree && ProfileCatalog.List(legacy).Any(row => row.Id == a.Id)
                && (File.Exists(legacy) ? Convert.ToHexString(File.ReadAllBytes(legacy)) : null) == rootBytes, "deleting another save leaves the open save's bytes and the selected id alone");
            Check(ProfileCatalog.Delete(legacy, d.Id, log.Add, open.FilePath).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == a.Id && HeldTree(SlotDir(legacy, a.Id)) == openTree, "a second delete in one session too");
            Check(open.SetGrindTier(GrindTiers.GrinderId) && open.GrindTier.Id == GrindTiers.GrinderId, "the open save still commits after deletes around it");
        }
        // Back at the unbound picker the rule is gone: same save, ordinary delete, selection cleared as ever.
        Check(ProfileCatalog.Delete(legacy, a.Id, log.Add).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == null, "unbound, the formerly open save deletes as today");
    }

    // The call the picker makes: state, then the bound path, with one log line either way.
    private static void PickerDeleteGate(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B"); var c = Add(legacy, "Save C");
        string open = ProfileCatalog.ProfilePathForId(legacy, a.Id), before = Tree(root);
        var log = new List<string>();
        Check(ProfileCatalog.DeleteBlock(legacy, a.Id, false, open, false) == "refused-current" && ProfileCatalog.DeleteBlock(legacy, b.Id, false, open, false) == null
            && ProfileCatalog.DeleteBlock(legacy, b.Id, false, open, true) == "refused-match" && ProfileCatalog.DeleteBlock(legacy, a.Id, true, null, false) == null
            && ProfileCatalog.DeleteBlock(legacy, b.Id, false, null, false) == "refused-bound", "the confirm's gate: open save, match, startup, and the closed unbound state");
        foreach (var (id, openPath, startup, match, outcome) in new[] { (a.Id, open, false, false, "refused-current"), (b.Id, open, false, true, "refused-match"), (b.Id, (string)null, false, false, "refused-bound") })
        {
            var refused = ProfileCatalog.DeleteFromPicker(legacy, id, log.Add, startup, openPath, match);
            Check(refused.Outcome == outcome && !refused.SlotRemoved && Tree(root) == before && log[^1].StartsWith("PROFILE_DELETE id=" + id) && log[^1].EndsWith("outcome=" + outcome), "picker delete refused and logged: " + outcome);
        }
        Check(ProfileCatalog.DeleteFromPicker(legacy, b.Id, log.Add, false, open, false).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == a.Id, "bound, the picker deletes another save");
        Check(ProfileCatalog.DeleteFromPicker(legacy, a.Id, log.Add, true, null, false).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == null, "unbound at startup, the picker deletes the selected save as today");
        Check(ProfileCatalog.DeleteFromPicker(legacy, c.Id, null, false, null, false).Outcome == "refused-bound", "a missing log never breaks the refusal");
    }

    // These read maintainer-only files (lab scripts, private docs), which a public checkout does not have.
    private static bool HasRepoFile(string relative)
    {
        return RepoRoot.FindMaintainerFile(relative) != null;
    }

    private static string RepoFile(string relative)
    {
        return File.ReadAllText(RepoRoot.FindMaintainerFile(relative) ?? throw new Exception("maintainer file missing: " + relative));
    }

    // Both script validators hold a literal field list: every element order the game can write must pass them. Pwsh is not on the Mac, so the lists are read, not run.
    private static void OptionsFieldsMatchTheScripts(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        string ps1 = RepoFile("windows/Runtime-Manifest.Common.ps1"), deck = RepoFile("tools/remote/deck/deck-remote.sh");
        string baseFields = System.Text.RegularExpressions.Regex.Match(ps1, @"\$optionFields = '([^']+)'").Groups[1].Value;
        string deckFields = System.Text.RegularExpressions.Regex.Match(deck, @"expected = \[([^\]]+)\]").Groups[1].Value.Replace("'", "");
        Check(baseFields.Length > 0 && deckFields == baseFields, "both script validators hold the same base field list");
        int number = 0;
        foreach (var options in new[] { new ProfileOptions(), new ProfileOptions { GrindTier = GrindTiers.GrinderId }, new ProfileOptions { AllDecks = true }, new ProfileOptions { GrindTier = GrindTiers.Relaxed6xId, AllDecks = true } })
        {
            var slot = Add(legacy, "Save " + ++number, options);
            var written = System.Xml.Linq.XDocument.Load(Path.Combine(SlotDir(legacy, slot.Id), "profile-options.xml")).Root!.Elements().Select(element => element.Name.LocalName).ToArray();
            string tail = string.Join(",", written.Skip(8));
            Check(string.Join(",", written.Take(8)) == baseFields, "the game writes the validators' base fields in order");
            Check(ps1.Contains(tail.Length == 0 ? "@($optionFields," : "\"$optionFields," + tail + "\"", StringComparison.Ordinal), "Assert-Options accepts what the game writes: " + tail);
            string deckTail = tail.Length == 0 ? "(expected," : "expected + [" + string.Join(",", written.Skip(8).Select(name => "'" + name + "'")) + "]";
            Check(deck.Contains(deckTail, StringComparison.Ordinal), "the Deck validator accepts what the game writes: " + tail);
        }
        foreach (var tier in GrindTiers.Selectable)
            Check(System.Text.RegularExpressions.Regex.IsMatch(tier.Id, "^[a-z0-9-]{1,32}$"), "pace id fits the validators' shape: " + tier.Id);
        foreach (string document in new[] { ps1, deck }) Check(document.Contains("[a-z0-9-]{1,32}", StringComparison.Ordinal), "the validator checks the pace's shape");
    }

    // The recipes' expected labels come from the rows the Pace page draws.
    private static void PaceHoursMatchTheRecipes()
    {
        string Hours(double hours) => hours.ToString(hours < 10 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture);
        string recipes = RepoFile("docs/agent-workflows/live-test-hooks.md");
        foreach (var tier in GrindTiers.Selectable)
            Check(recipes.Contains(tier.Name + " " + Hours(tier.HoursPerMetaDeck) + "/" + tier.HoursToHalfCollection + " h", StringComparison.Ordinal), "live-test-hooks.md states the Pace page's hours for " + tier.Name);
    }

    private static void DeleteWhileBoundToRoot(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        using (var profile = new LocalProfile(legacy)) profile.CreateDraft("Legacy", "HERO_08");
        var x = Add(legacy, "Save X"); var y = Add(legacy, "Save Y");
        string selected = ProfileCatalog.SelectedId(legacy), bytes = Convert.ToHexString(File.ReadAllBytes(legacy));
        Check(selected == "legacy" && ProfileCatalog.IsOpenSave(legacy, "legacy", selected, legacy), "the root save is the open one when bound to it");
        var log = new List<string>();
        Check(ProfileCatalog.Delete(legacy, "legacy", log.Add, legacy).Outcome == "refused-legacy", "the root row keeps its own refusal");
        Check(ProfileCatalog.Delete(legacy, x.Id, log.Add, legacy).Outcome == "deleted" && ProfileCatalog.SelectedId(legacy) == "legacy"
            && Convert.ToHexString(File.ReadAllBytes(legacy)) == bytes && ProfileCatalog.List(legacy).Any(row => row.Id == y.Id), "bound to the root, other saves delete and the root is untouched");
    }

    private static void DeleteCrashes(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B");
        var log = new List<string>();
        // Crash between the commit and the move: the slot is gone, the folder waits for Reconcile.
        ProfileCatalog.FaultHook = phase => { if (phase == "before-delete-move:" + a.Id) throw new IOException("crash before move"); };
        try
        {
            var deferred = ProfileCatalog.Delete(legacy, a.Id, log.Add);
            Check(deferred.Outcome == "move-deferred" && deferred.SlotRemoved && deferred.Moved == "none"
                && log[^1] == "PROFILE_DELETE id=" + a.Id + " name=Save A moved=none outcome=move-deferred", "move failure after the commit is move-deferred");
        }
        finally { ProfileCatalog.FaultHook = null; }
        Check(ProfileCatalog.List(legacy).Select(row => row.Id).SequenceEqual(new[] { b.Id }) && ProfileCatalog.SelectedId(legacy) == null
            && Directory.Exists(SlotDir(legacy, a.Id)) && DeletedNames(legacy).Length == 0
            && ProfileCatalog.ResolveActiveProfile(legacy) == Path.GetFullPath(legacy), "between commit and move the catalog is consistent and the folder is still in place");
        Add(legacy, "Save C");   // the unlisted folder does not block other work
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(!Directory.Exists(SlotDir(legacy, a.Id)) && DeletedNames(legacy).Single().StartsWith(a.Id + "-") && DeletedNames(legacy).Single().EndsWith("-orphan")
            && ProfileCatalog.DeletedFolders(legacy).SequenceEqual(DeletedNames(legacy))
            && log[^1] == "PROFILE_RECONCILE dropped=none moved=" + a.Id, "reconcile finishes the interrupted delete: " + log[^1]);
        Check(ProfileCatalog.List(legacy).Count == 2 && ProfileCatalog.List(legacy).All(row => row.Unavailable == null), "catalog consistent after reconcile");
    }

    private static void ReconcileBothWays(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        using (var profile = new LocalProfile(legacy)) profile.CreateDraft("Legacy", "HERO_08");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B");
        ProfileCatalog.CommitSelection(legacy, a.Id);
        string profiles = Path.Combine(root, "profiles"), orphan = new string('e', 32);
        Directory.Delete(SlotDir(legacy, a.Id), true);
        Directory.CreateDirectory(Path.Combine(profiles, orphan)); File.WriteAllText(Path.Combine(profiles, orphan, "profile.xml"), "x");
        foreach (string foreign in new[] { ".creating-" + new string('d', 32), "foreign", new string('c', 31) })
            Directory.CreateDirectory(Path.Combine(profiles, foreign));
        File.WriteAllText(Path.Combine(profiles, "notes.txt"), "keep");
        Directory.CreateDirectory(Path.Combine(DeletedDir(legacy), "foreign"));
        Check(ProfileCatalog.List(legacy).Single(row => row.Id == a.Id).Unavailable == "save file missing", "list alone does not reconcile");
        var log = new List<string>();
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(log.Count == 1 && log[0] == "PROFILE_RECONCILE dropped=" + a.Id + " moved=" + orphan, "reconcile logs what it dropped and moved: " + string.Join("|", log));
        Check(ProfileCatalog.SelectedId(legacy) == null && ProfileCatalog.List(legacy).Select(row => row.Id).OrderBy(id => id).SequenceEqual(new[] { "legacy", b.Id }.OrderBy(id => id))
            && !Directory.Exists(Path.Combine(profiles, orphan)) && DeletedNames(legacy).Any(name => name.StartsWith(orphan + "-") && name.EndsWith("-orphan"))
            && File.ReadAllText(Path.Combine(DeletedDir(legacy), DeletedNames(legacy).Single(name => name.StartsWith(orphan + "-")), "profile.xml")) == "x",
            "a missing-folder slot is dropped (clearing its selection), an unlisted hex folder moves to .deleted, the legacy slot stays");
        Check(Directory.Exists(Path.Combine(profiles, ".creating-" + new string('d', 32))) && Directory.Exists(Path.Combine(profiles, "foreign"))
            && Directory.Exists(Path.Combine(profiles, new string('c', 31))) && File.ReadAllText(Path.Combine(profiles, "notes.txt")) == "keep"
            && Directory.Exists(Path.Combine(DeletedDir(legacy), "foreign")), "reconcile leaves .creating-*, foreign names and .deleted entries alone");
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(log[^1] == "PROFILE_RECONCILE dropped=none moved=none", "reconcile is idempotent");
    }

    private static void ReconcileRefusesToGuess(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B");
        var log = new List<string>();
        string catalog = ProfileCatalog.CatalogPath(legacy);
        // Lost catalog: LoadUnlocked would fabricate an empty one; every real save would look orphaned. Nothing may change.
        File.Delete(catalog); File.Delete(catalog + ".bak"); File.Delete(catalog + ".tmp");
        string before = Tree(root);
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(Tree(root) == before && !File.Exists(catalog) && DeletedNames(legacy).Length == 0 && Directory.Exists(SlotDir(legacy, a.Id))
            && log.Single() == "PROFILE_RECONCILE dropped=none moved=none skipped=no-catalog", "a lost catalog makes reconcile do nothing: " + string.Join("|", log));
        // Corrupt catalog: the load throws; logged, nothing changed.
        File.WriteAllText(catalog, "garbage"); before = Tree(root); log.Clear();
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(Tree(root) == before && log.Single().Contains(" failed="), "a corrupt catalog makes reconcile log and do nothing");
        // Catalog intact but the profiles folder is gone: nothing is dropped.
        string cat2 = Path.Combine(root, "two"); Directory.CreateDirectory(cat2); string legacy2 = Path.Combine(cat2, "profile.xml");
        Add(legacy2, "Save A"); Directory.Delete(Path.Combine(cat2, "profiles"), true);
        before = Tree(cat2); log.Clear();
        ProfileCatalog.Reconcile(legacy2, log.Add);
        Check(Tree(cat2) == before && ProfileCatalog.List(legacy2).Count == 1 && log.Single() == "PROFILE_RECONCILE dropped=none moved=none skipped=no-profiles-folder",
            "a missing profiles folder drops nothing: " + string.Join("|", log));
        // Reconcile never purges.
        string cat3 = Path.Combine(root, "three"); Directory.CreateDirectory(cat3); string legacy3 = Path.Combine(cat3, "profile.xml");
        Add(legacy3, "Save A");
        for (int i = 0; i < 8; i++) Directory.CreateDirectory(Path.Combine(DeletedDir(legacy3), new string((char)('0' + i), 32) + "-" + Stamp(i)));
        ProfileCatalog.Reconcile(legacy3, log.Add);
        Check(DeletedNames(legacy3).Length == 8, "reconcile never purges .deleted");
    }

    private static void PurgeOldest(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A");
        string outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "precious.txt"), "keep");
        string deleted = DeletedDir(legacy); Directory.CreateDirectory(deleted);
        // Oldest names carry the newest mtimes: ordering must come from the name.
        for (int i = 0; i < 8; i++)
        {
            string dir = Path.Combine(deleted, new string((char)('0' + i), 32) + "-" + Stamp(i));
            Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "profile.xml"), "old");
            Directory.SetLastWriteTimeUtc(dir, new DateTime(2026, 6, 1).AddDays(7 - i));
        }
        bool linked = true;
        try { File.CreateSymbolicLink(Path.Combine(deleted, new string('0', 32) + "-" + Stamp(0), "link"), outside); }
        catch (Exception) { linked = false; }
        string[] foreign = { "notes", new string('9', 32), new string('9', 32) + "-bad", new string('9', 32) + "-" + Stamp(1) + "x", "x-" + Stamp(1) };
        foreach (string name in foreign) { Directory.CreateDirectory(Path.Combine(deleted, name)); File.WriteAllText(Path.Combine(deleted, name, "keep"), "keep"); }
        File.WriteAllText(Path.Combine(deleted, new string('8', 32) + "-" + Stamp(0)), "a file, not an entry");
        string orphanEntry = new string('b', 32) + "-" + Stamp(0) + "-orphan";
        Directory.CreateDirectory(Path.Combine(deleted, orphanEntry)); File.WriteAllText(Path.Combine(deleted, orphanEntry, "keep"), "keep");
        var log = new List<string>();
        Check(ProfileCatalog.Delete(legacy, a.Id, log.Add).Outcome == "deleted", "delete triggers the purge");
        var valid = DeletedNames(legacy).Where(name => name.Length == 52 && name[32] == '-' && name.EndsWith("Z") && char.IsDigit(name[33])).ToArray();
        string kept = string.Join(",", valid.Select(name => name.Substring(0, 2)));
        Check(valid.Length == (linked ? 6 : 5) && new[] { "7", "6", "5", "4" }.All(d => valid.Any(name => name.StartsWith(new string(d[0], 32) + "-")))
            && valid.Any(name => name.StartsWith(a.Id + "-")) && valid.Any(name => name.StartsWith(new string('0', 32) + "-")) == linked
            && !new[] { "1", "2", "3" }.Any(d => valid.Any(name => name.StartsWith(new string(d[0], 32) + "-"))), "newest 5 by folder-name stamp kept, oldest purged despite shuffled mtimes: " + kept);
        Check(File.ReadAllText(Path.Combine(deleted, orphanEntry, "keep")) == "keep" && ProfileCatalog.DeletedFolders(legacy).Contains(orphanEntry), "a reconcile-moved (-orphan) folder is never purged and is still listed");
        Check(foreign.All(name => File.ReadAllText(Path.Combine(deleted, name, "keep")) == "keep") && File.Exists(Path.Combine(deleted, new string('8', 32) + "-" + Stamp(0)))
            && File.ReadAllText(Path.Combine(outside, "precious.txt")) == "keep", "foreign names are never purged and nothing is followed out of the root");
        Check(log.Count(line => line.StartsWith("PROFILE_DELETE ")) == 1 && log.Any(line => line == "PROFILE_PURGE removed=" + (linked ? 3 : 4) + " skipped=" + (linked ? 1 : 0) + " failed=0"),
            "purge logs its tally: " + string.Join("|", log));
    }

    private static void DescribeReadOnly(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var plain = Add(legacy, "Plain"); var sandbox = Add(legacy, "Sandbox", new ProfileOptions { AllCards = true, InfiniteGold = true });
        var damaged = Add(legacy, "Damaged"); var badOptions = Add(legacy, "Bad options");
        string plainPath = ProfileCatalog.ProfilePathForId(legacy, plain.Id);
        var data = Read(plainPath); data.LadderState.StarIndex = 65; data.GoldBalance = 321; WriteProfile(plainPath, data);
        File.WriteAllText(ProfileCatalog.ProfilePathForId(legacy, damaged.Id), "<broken");
        File.AppendAllText(Path.Combine(SlotDir(legacy, badOptions.Id), "profile-options.xml"), "<Unknown/>");
        string before = Tree(root);
        var real = ProfileCatalog.Describe(legacy, plain.Id); var cheat = ProfileCatalog.Describe(legacy, sandbox.Id);
        var broken = ProfileCatalog.Describe(legacy, damaged.Id); var options = ProfileCatalog.Describe(legacy, badOptions.Id);
        Check(Tree(root) == before, "describe wrote nothing (bytes, mtimes and file list)");
        Check(real.Readable && !real.Sandbox && real.Name == "Plain" && real.Rank == LocalProfile.RankName(65) && real.Gold == 321 && real.DeckCount == Read(plainPath).CustomDecks.Count
            && !real.HasPendingMatch && real.CreatedUtc == plain.CreatedUtc, "describe summarises a real save");
        Check(cheat.Readable && cheat.Sandbox && cheat.Gold == ProfileCatalog.UnlimitedGold, "describe reports Sandbox and unlimited gold");
        Check(!broken.Readable && !broken.HasPendingMatch && !broken.PendingUnknown && broken.Name == "Damaged" && !options.Readable && options.Rank == LocalProfile.RankName(0),
            "damaged files are described as unreadable, never thrown");
        Reject(() => ProfileCatalog.Describe(legacy, new string('a', 32)), "describe of an unknown id");
        var log = new List<string>();
        var gone = ProfileCatalog.Delete(legacy, damaged.Id, log.Add);
        Check(gone.Outcome == "deleted" && gone.Moved.StartsWith("profiles/.deleted/" + damaged.Id + "-"), "a damaged save can still be deleted");
        Check(ProfileCatalog.Delete(legacy, badOptions.Id, log.Add).Outcome == "deleted", "a save with unreadable options can still be deleted");
    }
    private static string[] ProfileHashes(string legacy, string folder) => Directory.GetFiles(Path.Combine(DeletedDir(legacy), folder), "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    // Launch 1 loses the catalog, the user creates a save, launch 2 reconciles, and many deletes follow: no old healthy save may be purged.
    private static void LostCatalogNeverDestroys(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var old = new[] { Add(legacy, "Old A"), Add(legacy, "Old B"), Add(legacy, "Old C") };
        var before = old.ToDictionary(slot => slot.Id, slot => Directory.GetFiles(SlotDir(legacy, slot.Id), "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).Select(path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))).ToArray());
        string catalog = ProfileCatalog.CatalogPath(legacy);
        File.Delete(catalog); File.Delete(catalog + ".bak"); File.Delete(catalog + ".tmp");
        var log = new List<string>();
        ProfileCatalog.Reconcile(legacy, log.Add);   // launch 1: nothing to guess from
        Check(log.Single().Contains("skipped=no-catalog") && old.All(slot => Directory.Exists(SlotDir(legacy, slot.Id))), "launch 1 with no catalog changes nothing");
        var fresh = Add(legacy, "Fresh");             // the user creates a save: the catalog now lists only it
        ProfileCatalog.Reconcile(legacy, log.Add);   // launch 2: the old folders look unlisted
        Check(old.All(slot => !Directory.Exists(SlotDir(legacy, slot.Id)) && DeletedNames(legacy).Count(name => name.StartsWith(slot.Id + "-") && name.EndsWith("-orphan")) == 1),
            "launch 2 moves the unlisted folders to -orphan entries");
        for (int i = 0; i < 8; i++) { var temp = Add(legacy, "Temp " + i); Check(ProfileCatalog.Delete(legacy, temp.Id, log.Add).Outcome == "deleted", "temp delete " + i); }
        Check(ProfileCatalog.Delete(legacy, fresh.Id, log.Add).Outcome == "deleted", "delete the fresh save");
        foreach (var slot in old)
        {
            string folder = DeletedNames(legacy).Single(name => name.StartsWith(slot.Id + "-") && name.EndsWith("-orphan"));
            Check(ProfileHashes(legacy, folder).SequenceEqual(before[slot.Id]), "old save bytes survive every purge: " + slot.Name);
            Check(ProfileCatalog.DeletedFolders(legacy).Contains(folder), "orphan listed by save-catalog data: " + folder);
        }
        Check(DeletedNames(legacy).Count(name => !name.EndsWith("-orphan")) == ProfileCatalog.KeptDeleted, "ordinary deleted entries are still capped at 5");

        // Crash between commit and move, then reconcile, then purges: the -orphan folder survives.
        string crash = Path.Combine(root, "crash"); Directory.CreateDirectory(crash); string crashLegacy = Path.Combine(crash, "profile.xml");
        var victim = Add(crashLegacy, "Victim"); Add(crashLegacy, "Keeper");
        ProfileCatalog.FaultHook = phase => { if (phase.StartsWith("before-delete-move:")) throw new IOException("crash"); };
        try { Check(ProfileCatalog.Delete(crashLegacy, victim.Id, log.Add).Outcome == "move-deferred", "crash between commit and move"); }
        finally { ProfileCatalog.FaultHook = null; }
        ProfileCatalog.Reconcile(crashLegacy, log.Add);
        for (int i = 0; i < 7; i++) ProfileCatalog.Delete(crashLegacy, Add(crashLegacy, "Churn " + i).Id, log.Add);
        Check(DeletedNames(crashLegacy).Count(name => name.StartsWith(victim.Id + "-") && name.EndsWith("-orphan")) == 1, "a crashed delete's -orphan folder survives purges");
    }

    private static void PendingNeverFailsOpen(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var log = new List<string>();
        var held = Add(legacy, "Held"); var x = Add(legacy, "Seam");
        string heldPath = ProfileCatalog.ProfilePathForId(legacy, held.Id);
        // An I/O error reading profile.xml (here: another handle with FileShare.None; the FaultHook seam covers platforms that do not enforce it).
        string before = Tree(root);
        using (var hold = new FileStream(heldPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var summary = ProfileCatalog.Describe(legacy, held.Id);
            var refused = ProfileCatalog.Delete(legacy, held.Id, log.Add);
            Check(summary.PendingUnknown && !summary.Readable && refused.Outcome == "refused-unreadable" && Directory.Exists(SlotDir(legacy, held.Id))
                && ProfileCatalog.List(legacy).Any(row => row.Id == held.Id) && log[^1] == "PROFILE_DELETE id=" + held.Id + " name=Held moved=none outcome=refused-unreadable",
                "a locked profile.xml refuses the delete instead of reading as no pending");
        }
        Check(Tree(root) == before, "the refused delete changed nothing");
        Check(!ProfileCatalog.Describe(legacy, held.Id).PendingUnknown && ProfileCatalog.Delete(legacy, held.Id, log.Add).Outcome == "deleted", "released file deletes");
        ProfileCatalog.FaultHook = phase => { if (phase == "read-profile:" + x.Id) throw new UnauthorizedAccessException("scanner"); };
        try
        {
            Check(ProfileCatalog.Describe(legacy, x.Id).PendingUnknown && ProfileCatalog.Delete(legacy, x.Id, log.Add).Outcome == "refused-unreadable", "an access error refuses the delete");
        }
        finally { ProfileCatalog.FaultHook = null; }
        // Corrupt profile.xml stays deletable; a missing one reads .tmp the same way; a readable one ignores a stray .tmp.
        var corrupt = Add(legacy, "Corrupt"); File.WriteAllText(ProfileCatalog.ProfilePathForId(legacy, corrupt.Id), "<broken");
        Check(!ProfileCatalog.Describe(legacy, corrupt.Id).PendingUnknown && ProfileCatalog.Delete(legacy, corrupt.Id, log.Add).Outcome == "deleted", "corrupt XML stays deletable");
        LocalProfileData WithPending(string id) { var data = Read(ProfileCatalog.ProfilePathForId(legacy, id)); data.Pending = new LocalMatchRecord { Id = "p" }; return data; }
        var onlyTmp = Add(legacy, "Only tmp"); string tmpMain = ProfileCatalog.ProfilePathForId(legacy, onlyTmp.Id);
        WriteProfile(tmpMain + ".tmp", WithPending(onlyTmp.Id)); File.Delete(tmpMain);
        Check(ProfileCatalog.Describe(legacy, onlyTmp.Id).HasPendingMatch && ProfileCatalog.Delete(legacy, onlyTmp.Id, log.Add).Outcome == "refused-pending", "a pending match in profile.xml.tmp refuses when profile.xml is missing");
        File.WriteAllText(tmpMain + ".tmp", "<broken");
        Check(ProfileCatalog.Delete(legacy, onlyTmp.Id, log.Add).Outcome == "deleted", "a corrupt .tmp beside a missing profile.xml is deletable");
        var none = Add(legacy, "Nothing"); File.Delete(ProfileCatalog.ProfilePathForId(legacy, none.Id));
        Check(ProfileCatalog.Delete(legacy, none.Id, log.Add).Outcome == "deleted", "missing profile.xml and no .tmp is deletable");
        var stray = Add(legacy, "Stray"); string strayMain = ProfileCatalog.ProfilePathForId(legacy, stray.Id);
        WriteProfile(strayMain + ".tmp", WithPending(stray.Id));
        Check(!ProfileCatalog.Describe(legacy, stray.Id).HasPendingMatch && ProfileCatalog.Delete(legacy, stray.Id, log.Add).Outcome == "deleted", "a readable profile.xml beside a stray .tmp is the only truth");
    }

    private static void ReconcileNeedsAListing(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A"); var b = Add(legacy, "Save B");
        var log = new List<string>();
        string before = Tree(root);
        ProfileCatalog.FaultHook = phase => { if (phase == "before-reconcile-enumerate") throw new UnauthorizedAccessException("denied"); };
        try { ProfileCatalog.Reconcile(legacy, log.Add); }
        finally { ProfileCatalog.FaultHook = null; }
        Check(Tree(root) == before && log.Single().Contains(" failed=") && ProfileCatalog.List(legacy).Count == 2, "a failed listing aborts reconcile without dropping a slot");
        if (!OperatingSystem.IsWindows())
        {
            string profiles = Path.Combine(root, "profiles");
            File.SetUnixFileMode(profiles, UnixFileMode.None);
            try { ProfileCatalog.Reconcile(legacy, log.Add); }
            finally { File.SetUnixFileMode(profiles, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            Check(log[^1].Contains(" failed=") && !log[^1].Contains("dropped=" + a.Id) && ProfileCatalog.List(legacy).Count == 2 && Directory.Exists(SlotDir(legacy, a.Id)),
                "an unreadable profiles folder drops nothing: " + log[^1]);
        }
        Directory.Delete(SlotDir(legacy, b.Id), true);
        ProfileCatalog.Reconcile(legacy, log.Add);
        Check(log[^1] == "PROFILE_RECONCILE dropped=" + b.Id + " moved=none" && ProfileCatalog.List(legacy).Select(row => row.Id).SequenceEqual(new[] { a.Id }), "an id absent from a successful listing is dropped");
    }

    private static void PurgeSparesWhatItJustMoved(string root)
    {
        Directory.CreateDirectory(root); string legacy = Path.Combine(root, "profile.xml");
        var a = Add(legacy, "Save A");
        Directory.CreateDirectory(DeletedDir(legacy));
        // A clock set back: every earlier entry carries a future stamp, so the fresh one sorts oldest.
        for (int i = 0; i < 6; i++)
            Directory.CreateDirectory(Path.Combine(DeletedDir(legacy), new string((char)('1' + i), 32) + "-" + new DateTime(2099, 1, 1 + i).ToString("yyyyMMdd") + "T000000000Z"));
        var log = new List<string>();
        var result = ProfileCatalog.Delete(legacy, a.Id, log.Add);
        string entry = result.Moved.Substring(result.Moved.LastIndexOf('/') + 1);
        Check(result.Outcome == "deleted" && Directory.Exists(Path.Combine(DeletedDir(legacy), entry))
            && DeletedNames(legacy).Count(name => name.Length == 52) == ProfileCatalog.KeptDeleted, "the entry just moved survives a clock rollback and counts toward the five");
    }
}
