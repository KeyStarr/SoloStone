using System.Reflection;
using System.Xml.Serialization;

// Premium copies at the native boundary: collection stacks, deck contents and save, golden hero and favorites.
internal static class PremiumProjectionTests
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS premium projection: " + message);
    }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS premium projection: " + message); return; }
        throw new Exception("Expected refusal: " + message);
    }
    // Fresh V14 fixture: seed valid current ledgers directly; no historical-match migration/backfill.
    private static LocalProfile Fixture(string path)
    {
        using (var seed = new LocalProfile(path)) { seed.CreateDraft("Premium", "HERO_08"); }
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.ClassXp.ForEach(row => row.Xp = 25190);
        foreach (string hero in new[] { "HERO_08", "HERO_11" })
        {
            var portrait = LocalClassRewards.Portraits.Single(row => row.Hero == hero);
            data.PortraitGrants.Add(new LocalPortraitGrant { Hero = hero, Kind = "golden", CardId = hero });
            data.PortraitGrants.Add(new LocalPortraitGrant { Hero = hero, Kind = "honored", CardId = portrait.Honored });
            data.PremiumCollection.Add(new LocalCardCount { Id = hero, Premium = 1, Count = 1, Acquired = 1 });
            data.OwnedHeroSkins.Add(portrait.Honored);
        }
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        return new LocalProfile(path);
    }
    private static string Signature(IEnumerable<LocalDeckCard> cards) => string.Join(",", cards.Select(card => card.Id + "/" + card.Premium));

    internal static void Run()
    {
        // Native SetCounts overwrites duplicate keys: one stack per (card, version), summed.
        var packet = NativeUIProbe.CollectionPacket(new[] {
            new LocalCardCount { Id = "CATA_458", Count = 1 }, new LocalCardCount { Id = "CATA_458", Count = 2 },
            new LocalCardCount { Id = "CATA_458", Premium = 1, Count = 2 }, new LocalCardCount { Id = "CATA_458", Premium = 1, Count = 1 },
            new LocalCardCount { Id = "CATA_458", Premium = 3, Count = 0 }, new LocalCardCount { Id = "CATA_452", Premium = 2, Count = 1 } });
        Check(packet.Stacks.Count == 3 && packet.Stacks.Select(s => (s.CardDef.Asset, s.CardDef.Premium)).Distinct().Count() == 3
            && packet.Stacks.Single(s => s.CardDef.Premium == 0).Count == 3 && packet.Stacks.Single(s => s.CardDef.Premium == 1).Count == 3
            && packet.Stacks.Single(s => s.CardDef.Premium == 2).NumSeen == 1 && packet.Stacks.All(s => s.CardDef.Premium != 3),
            "premium stacks aggregate per key, empty versions are omitted");
        // Saved seen copies become NumSeen (native NEW glow and class-tab counts), clamped to the stack; no row = seen.
        var seen = NativeUIProbe.CollectionPacket(new[] {
            new LocalCardCount { Id = "CATA_458", Count = 2 }, new LocalCardCount { Id = "CATA_458", Premium = 1, Count = 1 },
            new LocalCardCount { Id = "CATA_452", Count = 1 } },
            (id, premium) => id == "CATA_458" && premium == 0 ? 1 : id == "CATA_452" ? -3 : int.MaxValue).Stacks;
        Check(seen.Single(s => s.CardDef.Premium == 0 && s.Count == 2).NumSeen == 1
            && seen.Single(s => s.CardDef.Premium == 1).NumSeen == 1
            && seen.Single(s => s.CardDef.Asset == GameUtils.TranslateCardIdToDbId("CATA_452")).NumSeen == 0,
            "seen copies project to NumSeen per version");

        using var cosmetics = new CosmeticsFixture();
        string root = Path.Combine(Path.GetTempPath(), "premium-projection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
#if ENGINE_SOURCE
            using (var welcome = new LocalProfile(Path.Combine(root, "welcome.xml")))
            {
                Check(welcome.GrantWelcomeIfNew(), "fresh welcome commits before native collection projection");
                var welcomeStacks = NativeUIProbe.CollectionPacket(NativeUIProjection.CollectionStacks(welcome)).Stacks;
                foreach (var grant in welcome.Welcome.Cards.Where(grant => grant.Count > 0))
                    Check(welcomeStacks.Single(stack => stack.CardDef.Asset == GameUtils.TranslateCardIdToDbId(grant.CardId)
                        && stack.CardDef.Premium == grant.Premium).Count == grant.Count
                        && NativeUIProjection.IsNativeCollectible(grant.CardId, welcome),
                        "owned welcome stack reaches native SetCounts: " + grant.CardId + "/" + grant.Premium);
                Check(welcomeStacks.Single(stack => stack.CardDef.Asset == GameUtils.TranslateCardIdToDbId("BE_036")).Count == 1
                    && welcome.OwnedCount("BE_036", 1) == 0, "M.O.T.H.E.R. projects exactly one Normal copy, without invented Golden ownership");
            }
#endif
            string path = Path.Combine(root, "profile.xml");
            using var profile = Fixture(path);
            var stacks = NativeUIProbe.CollectionPacket(NativeUIProjection.CollectionStacks(profile)).Stacks;
            int hero = GameUtils.TranslateCardIdToDbId("HERO_08");
            Check(stacks.Count(s => s.CardDef.Asset == hero && s.CardDef.Premium == 0) == 1
                && stacks.Single(s => s.CardDef.Asset == hero && s.CardDef.Premium == 1).Count == 1
                && stacks.GroupBy(s => (s.CardDef.Asset, s.CardDef.Premium)).All(group => group.Count() == 1),
                "golden vanilla hero is its own premium stack next to the Normal hero");
            var reward = LocalClassRewards.Rewards.First(row => row.Hero == "HERO_08" && row.Premium == 1);
            Check(stacks.Single(s => s.CardDef.Asset == GameUtils.TranslateCardIdToDbId(reward.CardId) && s.CardDef.Premium == 1).Count == reward.Copies
                && NativeUIProjection.TotalOwned(profile)[reward.CardId] == profile.TotalOwnedCount(reward.CardId),
                "class reward copies project as premium stacks and native totals span versions");

            // Start-time ownership spans versions: a deck whose copies are all Golden must pass (IsDeckComplete checks versions).
            var goldenOnly = LocalClassRewards.Rewards.First(row => row.Hero == "HERO_08" && row.Premium == 1 && row.Copies == 2
                && profile.OwnedCount(row.CardId) == 0 && profile.OwnedCount(row.CardId, 1) == 2);
            var startIds = LocalDecks.Get("mage").CardIds.Take(28).Concat(new[] { goldenOnly.CardId, goldenOnly.CardId }).ToArray();
            var startDeck = new LocalCustomDeck { Id = profile.CustomDecks.First().Id, Name = "Golden only", Hero = "HERO_08", Cards = startIds.ToList() };
            var startResolved = LocalProfile.LedgerDeck(startDeck.Name, startDeck.Hero, startIds, null, false);
            NativeUIProjection.ValidateCustomStart(profile, startDeck.Id, startDeck, startResolved);
            Check(true, "personal deck of Golden-only copies passes the start-time ownership check");
            Reject(() => NativeUIProjection.ValidateCustomSelection(startDeck.Id, startDeck.Hero, startIds, startDeck.Hero, startIds,
                profile.Collection.ToDictionary(pair => pair.Key, pair => pair.Value)), "Normal-only ownership refuses the same deck");

            // Shop grants compare Normal ledger with native Normal; premium stacks must not leak into the difference.
            var normal = new Dictionary<string, int> { ["A"] = 1, ["B"] = 3, ["C"] = 2 };
            var nativeNormal = new Dictionary<string, int> { ["A"] = 1, ["B"] = 0, ["C"] = 1 };
            var shop = NativeUIProjection.ShopGrantCounts(normal, new[] { "A", "B", "C", "B" }, id => nativeNormal[id]);
            Check(shop.Count == 2 && shop["B"] == 3 && shop["C"] == 1,
                "shop refresh adds the Normal difference even when native also holds Golden copies of the card");
            Reject(() => NativeUIProjection.ShopGrantCounts(normal, new[] { "A" }, id => 2), "native Normal above the ledger is refused");

            // Settlement-granted premium copies reach native per version, add-only, heroes excluded.
            var ownedRows = new[] {
                new LocalCardCount { Id = "A", Premium = 1, Count = 2 }, new LocalCardCount { Id = "A", Premium = 2, Count = 1 },
                new LocalCardCount { Id = "B", Premium = 3, Count = 1 }, new LocalCardCount { Id = "HERO_08", Premium = 1, Count = 1 },
                new LocalCardCount { Id = "A", Count = 4 }, new LocalCardCount { Id = "C", Premium = 1, Count = 2 } };
            var nativeHas = new Dictionary<(string, int), int> { [("A", 1)] = 1, [("A", 2)] = 1, [("C", 1)] = 5 };
            var deltas = NativeUIProjection.PremiumGrantDeltas(ownedRows, (id, premium) => nativeHas.GetValueOrDefault((id, premium)), id => id == "HERO_08");
            Check(deltas.Count == 2 && deltas[0] == ("A", 1, 1) && deltas[1] == ("B", 3, 1),
                "premium refresh adds only the missing copies per version, never Normal, heroes or removals");

            // Favorites: the golden hero shows only for the vanilla hero; alternate skins stay Normal.
            Check(NativeUIProjection.FavoritePremium(profile, "HERO_08", "HERO_08") == 1
                && NativeUIProjection.FavoritePremium(profile, "HERO_01", "HERO_01") == 0
                && NativeUIProjection.FavoritePremium(profile, "HERO_11", "HERO_11a") == 0, "favorite premium follows the owned golden hero");
            var select = typeof(NativeUIProjection).GetMethod("SelectHeroVersion", BindingFlags.Static | BindingFlags.NonPublic)!;
            void Select(string heroClass, string id, bool favorite, int premium) {
                try { select.Invoke(null, new object[] { profile, heroClass, id, favorite, premium }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { throw error.InnerException; }
            }
            Select("HERO_11", "HERO_11a", true, 0);
            Check(NativeUIProjection.FavoritePremium(profile, "HERO_11", NativeUIProjection.FavoriteHero(profile, "HERO_11")) == 0,
                "honored skin favorite keeps Normal");
            Select("HERO_11", "HERO_11", true, 1);
            Check(NativeUIProjection.FavoritePremium(profile, "HERO_11", NativeUIProjection.FavoriteHero(profile, "HERO_11")) == 1,
                "golden hero favorite keeps its premium");
            Reject(() => Select("HERO_01", "HERO_01", true, 1), "golden hero not owned is refused");
            Reject(() => Select("HERO_11", "HERO_11", true, 2), "diamond hero is refused");
            Reject(() => Select("HERO_11", "HERO_11a", true, 1), "alternate skin has no golden version");

            // Deck contents: one row per (card, version).
            var rewards = LocalClassRewards.Rewards.Where(row => row.Hero == "HERO_08" && row.Premium == 1 && row.Copies == 2).Take(2).ToArray();
            string a = rewards[0].CardId, b = rewards[1].CardId;
            var entries = new List<LocalDeckCard> {
                new() { Id = a, Premium = 1 }, new() { Id = a }, new() { Id = b, Premium = 1 }, new() { Id = b, Premium = 1 } };
            var rows = NativeUIProjection.DeckRows(entries);
            Check(rows.Count == 3 && rows.Single(r => r.Id == b && r.Premium == 1).Qty == 2 && rows.Single(r => r.Id == a && r.Premium == 0).Qty == 1,
                "deck contents keep each version as its own row");

            // Native editor save: exact versions survive, incomplete Normal copies stay savable.
            var draft = profile.CustomDecks.First(deck => deck.Hero == "HERO_08");
            var manager = new CollectionManager { PremiumAware = true };
            CollectionManager.Instance = manager;
            foreach (var card in profile.OwnedCards) {
                if (card.Premium == 0) manager.Counts[card.Id] = card.Count;
                else manager.PremiumCounts[(card.Id, (TAG_PREMIUM)card.Premium)] = card.Count;
            }
            manager.Counts.Remove(a); // Normal copy of A is missing: it loads as a Normal ghost.
            var native = new CollectionDeck();
            foreach (var card in entries) native.AddCard(card.Id, (TAG_PREMIUM)card.Premium, true);
            Check(NativeUIProbe.DeckEntries(native).Count(card => card.Id == b && card.Premium == 1) == 2
                && NativeUIProbe.DeckEntries(native).Count(card => card.Id == a && card.Premium == 0) == 1
                && native.GetSlots().Count(slot => slot.CardID == a && !slot.Owned) == 1, "native deck holds premium copies and a Normal ghost");
            NativeUIProjection.SaveNativeDraft(profile, draft.Id, draft.Name, draft.Hero, NativeUIProbe.DeckEntries(native),
                NativeUIProbe.ReadSideboards(native), draft.SortOrder);
            string saved = Signature(profile.GetDraft(draft.Id).CardEntries);
            Check(saved == Signature(NativeUIProbe.DeckEntries(native)) && profile.GetDraft(draft.Id).CardEntries.Count(card => card.Premium == 1) == 3
                && profile.GetDraft(draft.Id).CardEntries.Count == 4,
                "native save keeps every copy's version, incomplete deck included");
            Reject(() => NativeUIProjection.SaveNativeDraft(profile, draft.Id, draft.Name, draft.Hero,
                new List<LocalDeckCard> { new() { Id = a, Premium = 3 } }, Array.Empty<LocalSideboard>(), draft.SortOrder), "unowned version is not saved");
            Check(saved == Signature(profile.GetDraft(draft.Id).CardEntries), "refused save leaves the draft intact");
            profile.Dispose();
            using (var again = new LocalProfile(path))
                Check(Signature(again.GetDraft(draft.Id).CardEntries) == saved, "premium deck survives restart");
            using var reloaded = new LocalProfile(path);

            // Normal-only decks keep the id path (no ownership check on missing copies).
            NativeUIProjection.SaveNativeDraft(reloaded, draft.Id, draft.Name, draft.Hero, new List<LocalDeckCard> { new() { Id = a } },
                Array.Empty<LocalSideboard>(), draft.SortOrder);
            Check(reloaded.GetDraft(draft.Id).CardEntries.Single().Premium == 0, "Normal-only save is unchanged");

            // Template filling must not re-version Normal copies to the best owned one.
            manager.Counts[a] = 1;
            foreach (bool normalOwned in new[] { true, false })
            {
                manager.Counts[a] = normalOwned ? 1 : 0;
                var saved2 = new LocalCustomDeck { Id = 1, Name = "Mixed", Hero = "HERO_08",
                    CardEntries = new() { new() { Id = a }, new() { Id = a, Premium = 1 } } };
                var damaged = new CollectionDeck();
                NativeUIProbe.ProjectMissingDraftCards(damaged, saved2);
                var slot = damaged.GetSlots().Single(row => row.Owned);
                Check(damaged.GetCardsWithCardID().Count == 2 && slot.GetCount(TAG_PREMIUM.GOLDEN) == (normalOwned ? 1 : 2)
                    && slot.GetCount(TAG_PREMIUM.NORMAL) == (normalOwned ? 1 : 0),
                    normalOwned ? "restore puts the saved Normal and Golden copies back" : "restore falls back to an owned version when the Normal copy is gone");
            }

#if ENGINE_SOURCE
            // Sideboards carry versions through native add and read-back.
            var side = reloaded.CreateDraft("Premium sideboard", "HERO_04");
            var candidate = reloaded.OwnedCards.First(row => row.Premium > 0 && SabberStoneCore.Actions.DeckbuildingRules.Eligible("JAIL_397",
                SabberStoneCore.Model.Cards.FromId(row.Id), SabberStoneCore.Enums.FormatType.FT_STANDARD));
            manager.PremiumCounts[(candidate.Id, (TAG_PREMIUM)candidate.Premium)] = candidate.Count;
            var board = new LocalSideboard { Source = "JAIL_397", CardEntries = new() { new() { Id = candidate.Id, Premium = candidate.Premium } } };
            var projected = new CollectionDeck();
            NativeUIProbe.ProjectSideboards(projected, new[] { board });
            var read = NativeUIProbe.ReadSideboards(projected);
            Check(read.Single().Source == "JAIL_397" && Signature(read.Single().CardEntries) == Signature(board.CardEntries),
                "sideboard projection keeps the saved version through native add");
            NativeUIProjection.SaveNativeDraft(reloaded, side.Id, side.Name, side.Hero, new List<LocalDeckCard> { new() { Id = "JAIL_397" } }, read, 1);
            Check(Signature(reloaded.GetDraft(side.Id).Sideboards.Single().CardEntries) == Signature(board.CardEntries),
                "sideboard version round-trips through native save");
            reloaded.Dispose();
            using (var again = new LocalProfile(path))
                Check(Signature(again.GetDraft(side.Id).Sideboards.Single().CardEntries) == Signature(board.CardEntries), "sideboard version survives restart");
#endif
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
