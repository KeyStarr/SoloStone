using System.Xml.Serialization;

internal static class CraftingProjectionTests
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS crafting projection: " + message);
    }
    private static string Xml(LocalCustomDeck saved)
    {
        using var writer = new StringWriter();
        new XmlSerializer(typeof(LocalCustomDeck)).Serialize(writer, saved);
        return writer.ToString();
    }
    internal static void Run()
    {
        var duplicatePacket = NativeUIProbe.CollectionPacket(new[] {
            new KeyValuePair<string, int>("CATA_458", 1), new KeyValuePair<string, int>("CATA_458", 2) });
        Check(duplicatePacket.Stacks.Count == 1 && duplicatePacket.Stacks.Single().Count == 3,
            "duplicate collection keys aggregate before native SetCounts");
        var packet = NativeUIProbe.CollectionPacket(new Dictionary<string, int> { ["CATA_452"] = 0, ["CATA_458"] = 1 });
        Check(packet.Stacks.Count == 1 && packet.Stacks[0].CardDef.Asset == GameUtils.TranslateCardIdToDbId("CATA_458")
            && packet.Stacks[0].Count == 1 && packet.Stacks[0].NumSeen == 1,
            "restart omits zero-owned rows while retaining positive counts");
        CollectionManager.Instance = new();
        CollectionManager.Instance.Counts["EX1_015"] = 1;
        var projected = new CollectionDeck();
        NativeUIProbe.ProjectSideboards(projected, new[] { new LocalSideboard { Source = "JAIL_397", Cards = new() { "EX1_015", "EX1_015" } } });
        var sideboardSlots = projected.Sideboards["JAIL_397"].GetSlots();
        Check(sideboardSlots.Count == 2 && sideboardSlots.Single(slot => slot.Owned).Count == 1
            && sideboardSlots.Single(slot => !slot.Owned).Count == 1
            && projected.Rows().Single().Cards.SequenceEqual(new[] { "EX1_015", "EX1_015" }),
            "sideboard projection retains owned and missing copies through native add");
#if ENGINE_SOURCE
        string root = Path.Combine(Path.GetTempPath(), "craft-projection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
        foreach (bool loaner in new[] { false, true })
        foreach (bool fabled in new[] { false, true })
        {
            var saved = new LocalCustomDeck { Id = loaner ? 910000002 : 910000010, Name = loaner ? "Loaner: Recipe" : "Recipe", Hero = fabled ? "HERO_07" : "HERO_04",
                Cards = fabled ? new[] { "TIME_005" }.Concat(SabberStoneCore.Actions.FabledRules.Bundle("TIME_005")).Append("CS2_182").ToList() : new() { "JAIL_397", "CS2_182" },
                Sideboards = fabled ? new() : new() { new() { Source = "JAIL_397", Cards = new() { "EX1_015" } } } };
            string path = Path.Combine(root, $"{loaner}-{fabled}.xml");
            using var profile = new LocalProfile(path);
            if (!loaner)
            {
                var draft = profile.CreateDraft(saved.Name, saved.Hero);
                profile.SaveDraft(draft.Id, saved.Name, saved.Hero, saved.Cards, draft.SortOrder, saved.Sideboards);
                saved = profile.GetDraft(draft.Id);
            }
            string before = Xml(saved);
            byte[] bytes = loaner ? Array.Empty<byte>() : File.ReadAllBytes(path);
            NativeUIProbe.SetCraftingRecipe(profile, saved, loaner);
            var native = new CollectionDeck(saved);
            var baseline = new CollectionDeck(saved);
            // Mirror native removal: Fabled loses primary+bundle; sideboard owner keeps its ghost but loses sideboard.
            if (fabled) native.Cards.RemoveAll(id => id != "CS2_182");
            else native.Sideboards.Clear();
            CollectionManager.Instance.Working[saved.Id] = native;
            CollectionManager.Instance.Baselines[saved.Id] = baseline;
            NativeUIProbe.ReprojectCraftingDecks(new[] { fabled ? "TIME_005" : "JAIL_397" });
            Check(native.Cards.SequenceEqual(saved.Cards) && NativeUIProbe.SideboardSignature(native.Rows()) == NativeUIProbe.SideboardSignature(saved.Sideboards)
                && baseline.Cards.SequenceEqual(saved.Cards), "restore saved/loaner primary, bundle and sideboard: " + (loaner, fabled));
            // Editor save sees the original recipe; no reduced recipe reaches persistence.
            EditorSave(loaner ? null : profile, native, saved);
            Check(!NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved) && Xml(saved) == before
                && (loaner || bytes.SequenceEqual(File.ReadAllBytes(path))), "restored editor save preserves durable recipe bytes");
            if (fabled) native.Cards.RemoveAll(id => id != "CS2_182"); else native.Sideboards.Clear();
            native.FailProjection = true;
            bool failed = false;
            try { NativeUIProbe.ProjectCraftingRecipe(native, baseline, saved); } catch (IOException) { failed = true; }
            EditorSave(loaner ? null : profile, native, saved);
            Check(failed && NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved) && Xml(saved) == before
                && (loaner || bytes.SequenceEqual(File.ReadAllBytes(path))),
                "failed reprojection refuses reconciliation-only editor save");
            native.Cards.Remove("CS2_182");
            Check(!NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved), "genuine user removal retains editor save path");
            if (!loaner)
            {
                EditorSave(loaner ? null : profile, native, saved);
                Check(!bytes.SequenceEqual(File.ReadAllBytes(path)), "genuine user removal reaches durable editor save");
            }
            native.FailProjection = false;
            NativeUIProbe.ProjectCraftingRecipe(native, baseline, saved);
            Check(!NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved), "successful retry clears reconciliation guard");
        }
        foreach (bool initiallyOwned in new[] { false, true })
        {
            string splitPath = Path.Combine(root, $"split-{initiallyOwned}.xml");
            using var profile = new LocalProfile(splitPath);
            var draft = profile.CreateDraft("Two copies", "HERO_04");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "CS2_182", "CS2_182" }, draft.SortOrder);
            var saved = profile.GetDraft(draft.Id);
            byte[] before = File.ReadAllBytes(splitPath);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var native = new CollectionDeck(saved);
            native.GetSlots().Single().Owned = initiallyOwned;
            var manager = CollectionManager.Get();
            manager.Working[saved.Id] = manager.Edited = native;
            manager.Counts["CS2_182"] = initiallyOwned ? 2 : 0;
            NativeUIProbe.ApplyCraftingChanges(new() { new() { cardID = "CS2_182", premium = TAG_PREMIUM.NORMAL,
                count = 1, modificationType = initiallyOwned ? CollectionManager.CardModification.ModificationType.Remove : CollectionManager.CardModification.ModificationType.Add } });
            Check(manager.SawCraftingSynchronize && !NativeUIProbe.craftingSynchronize,
                "native reconcile runs inside the crafting-synchronize window, which closes afterwards");
            var slots = native.GetSlots();
            Check(slots.Count == 2 && slots.Single(slot => slot.Owned).Count == 1 && slots.Single(slot => !slot.Owned).Count == 1,
                "two-copy slot splits into one owned and one missing after " + (initiallyOwned ? "disenchant" : "craft"));
            Check(native.GetCardsWithCardID().OrderBy(id => id).SequenceEqual(new[] { "CS2_182", "CS2_182" })
                && saved.Cards.SequenceEqual(new[] { "CS2_182", "CS2_182" }), "ownership split preserves recipe ids and counts");
            EditorSave(profile, native, saved);
            Check(before.SequenceEqual(File.ReadAllBytes(splitPath)), "ownership split leaves durable recipe unchanged");
            if (!initiallyOwned)
            {
                NativeUIProbe.ApplyCraftingChanges(new() { new() { cardID = "CS2_182", premium = TAG_PREMIUM.NORMAL,
                    count = 1, modificationType = CollectionManager.CardModification.ModificationType.Add } });
                Check(native.GetSlots().All(slot => slot.Owned) && native.GetCardsWithCardID().Count == 2,
                    "crafting final missing copy clears every ghost without dropping cards");
                EditorSave(profile, native, saved);
                Check(before.SequenceEqual(File.ReadAllBytes(splitPath)), "fully owned recipe remains unchanged on save");
            }
        }
        foreach (bool removeChanged in new[] { false, true })
        foreach (bool fabled in new[] { false, true })
        foreach (bool failNotification in new[] { false, true })
        {
            string editedPath = Path.Combine(root, $"edited-{removeChanged}-{fabled}-{failNotification}.xml");
            using var profile = new LocalProfile(editedPath);
            // The unsaved Golden slots below are owned: since e81a1caca crafting re-projects an edited deck only when a
            // slot's version is no longer owned (AssignOwnedPremiums), so owned exact premiums must survive untouched.
            profile.CommitChange(next => {
                foreach (string id in new[] { fabled ? "TIME_005" : "JAIL_397", "EX1_015" })
                    next.PremiumCollection.Add(new LocalCardCount { Id = id, Premium = 1, Count = 1, Acquired = 1 });
                return true;
            });
            var draft = profile.CreateDraft("Edited", fabled ? "HERO_07" : "HERO_04");
            var cards = fabled ? new[] { "TIME_005" }.Concat(SabberStoneCore.Actions.FabledRules.Bundle("TIME_005")).Append("CS2_182").ToList()
                : new List<string> { "JAIL_397", "CS2_182" };
            var sides = fabled ? new List<LocalSideboard>() : new() { new() { Source = "JAIL_397", Cards = new() { "EX1_015" } } };
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, cards, draft.SortOrder, sides);
            var saved = profile.GetDraft(draft.Id);
            string before = Xml(saved);
            byte[] bytes = File.ReadAllBytes(editedPath);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var manager = CollectionManager.Get();
            var native = new CollectionDeck(saved);
            native.Cards.Remove("CS2_182"); // Unsaved removal must never be restored from saved recipe.
            string changed = fabled ? "TIME_005" : "JAIL_397";
            if (removeChanged)
            {
                native.Cards.Clear();
                native.Sideboards.Clear();
            }
            else
            {
                native.GetSlots().First().SetCount(1, TAG_PREMIUM.GOLDEN);
                native.GetSlots().First().NumSubCards = fabled ? 7 : 0;
                if (!fabled) native.Sideboards[changed].GetSlots().First().SetCount(1, TAG_PREMIUM.GOLDEN);
            }
            manager.Working[saved.Id] = native;
            manager.Baselines[saved.Id] = new(saved);
            manager.Edited = native;
            manager.FailNotification = failNotification;
            manager.Counts[changed] = 1;
            string working = native.Contents();
            int refreshes = CollectionDeckTray.Refreshes;
            int indicatorRefreshes = CollectionDeckTray.IndicatorRefreshes;
            bool failed = false;
            try { NativeCrafting.Remove(changed); } catch (IOException) { failed = true; }
            Check(failed == failNotification && native.Contents() == working && !native.Cards.Contains("CS2_182"),
                "crafting preserves unsaved removal, exact premiums, bundle and sideboard, including notification failure: " + (removeChanged, fabled, failNotification));
            Check((removeChanged || !native.GetSlots().First().Owned) && CollectionDeckTray.Refreshes == refreshes + 1,
                "edited deck refreshes ownership and tray after restoration");
            Check(CollectionDeckTray.IndicatorRefreshes == indicatorRefreshes + 1, "edited restore refreshes deck-list missing indicators");
            Check(Xml(saved) == before && bytes.SequenceEqual(File.ReadAllBytes(editedPath))
                && !NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved), "crafting leaves saved recipe and ordinary edit save path unchanged");
        }
        foreach (bool edited in new[] { false, true })
        {
            using var profile = new LocalProfile(Path.Combine(root, $"upgrade-{edited}.xml"));
            const string card = "CATA_452";
            string hero = LocalDecks.PlayerHeroes.First(id => SabberStoneCore.Model.Cards.FromId(id).Class == SabberStoneCore.Model.Cards.FromId(card).Class);
            var draft = profile.CreateDraft("Upgrade", hero);
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { card, card, "CS2_182" }, draft.SortOrder);
            var saved = profile.GetDraft(draft.Id);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var manager = CollectionManager.Get();
            manager.PremiumAware = true;
            var native = new CollectionDeck(saved);
            var baseline = new CollectionDeck(saved);
            manager.Working[saved.Id] = native; manager.Baselines[saved.Id] = baseline;
            if (edited) { manager.Edited = native; native.Cards.Remove("CS2_182"); }
            for (int golden = 1; golden <= 2; golden++)
            {
                int normal = 2 - golden;
                profile.CommitChange(next => {
                    next.Collection.RemoveAll(row => row.Id == card);
                    next.Collection.Add(new LocalCardCount { Id = card, Count = normal, Acquired = 2 });
                    next.PremiumCollection.RemoveAll(row => row.Id == card && row.Premium == 1);
                    next.PremiumCollection.Add(new LocalCardCount { Id = card, Premium = 1, Count = golden, Acquired = golden });
                    LocalCrafting.ReconcileUpgradeDeck(next.CustomDecks.Single(row => row.Id == saved.Id), card, normal, golden);
                    return true;
                });
                manager.Counts[card] = normal; manager.PremiumCounts[(card, TAG_PREMIUM.GOLDEN)] = golden;
                NativeUIProbe.RefreshUpgradedDecks(card);
                foreach (var deck in new[] { native, baseline })
                {
                    var slots = deck.GetSlots().Where(slot => slot.CardID == card).ToArray();
                    Check(slots.All(slot => slot.Owned) && slots.Sum(slot => slot.GetCount(TAG_PREMIUM.GOLDEN)) == golden
                        && slots.Sum(slot => slot.GetCount(TAG_PREMIUM.NORMAL)) == normal,
                        "upgrade immediately restores exact owned premiums in working/baseline deck: " + (edited, golden));
                }
                Check(native.GetCardsWithCardID().Contains("CS2_182") == !edited && baseline.GetCardsWithCardID().Contains("CS2_182"),
                    "upgrade projection preserves unsaved editor removal: " + (edited, golden));
            }
        }
        foreach (bool edited in new[] { false, true })
        {
            using var profile = new LocalProfile(Path.Combine(root, $"upgrade-side-{edited}.xml"));
            string card = LocalShop.StandardCollectionCards.First(candidate => candidate.Rarity != SabberStoneCore.Enums.Rarity.LEGENDARY
                && SabberStoneCore.Actions.DeckbuildingRules.Eligible("JAIL_831", candidate, SabberStoneCore.Enums.FormatType.FT_WILD)).Id;
            var draft = profile.CreateDraft("Sideboard upgrade", "HERO_05");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "JAIL_831" }, draft.SortOrder,
                new[] { new LocalSideboard { Source = "JAIL_831", Cards = new() { card, card } } });
            var saved = profile.GetDraft(draft.Id);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var manager = CollectionManager.Get(); manager.PremiumAware = true;
            var native = new CollectionDeck(saved); var baseline = new CollectionDeck(saved);
            manager.Working[saved.Id] = native; manager.Baselines[saved.Id] = baseline;
            if (edited) manager.Edited = native;
            for (int golden = 1; golden <= 2; golden++)
            {
                int normal = 2 - golden;
                profile.CommitChange(next => {
                    next.Collection.RemoveAll(row => row.Id == card);
                    next.Collection.Add(new LocalCardCount { Id = card, Count = normal, Acquired = 2 });
                    next.PremiumCollection.RemoveAll(row => row.Id == card && row.Premium == 1);
                    next.PremiumCollection.Add(new LocalCardCount { Id = card, Premium = 1, Count = golden, Acquired = golden });
                    LocalCrafting.ReconcileUpgradeDeck(next.CustomDecks.Single(row => row.Id == saved.Id), card, normal, golden);
                    return true;
                });
                manager.Counts[card] = normal; manager.PremiumCounts[(card, TAG_PREMIUM.GOLDEN)] = golden;
                NativeUIProbe.RefreshUpgradedDecks(card);
                foreach (var deck in new[] { native, baseline })
                {
                    var slots = deck.GetAllSideboards()["JAIL_831"].GetSlots().Where(slot => slot.CardID == card).ToArray();
                    Check(slots.All(slot => slot.Owned) && slots.Sum(slot => slot.GetCount(TAG_PREMIUM.GOLDEN)) == golden
                        && slots.Sum(slot => slot.GetCount(TAG_PREMIUM.NORMAL)) == normal,
                        "upgrade immediately refreshes owned sideboard premiums: " + (edited, golden));
                }
            }
        }
        foreach (bool edited in new[] { false, true })
        {
            using var profile = new LocalProfile(Path.Combine(root, $"pack-premium-{edited}.xml"));
            const string card = "CATA_213";
            profile.CommitChange(next => { next.Collection.Add(new LocalCardCount { Id = card, Count = 1, Acquired = 1 }); return true; });
            string hero = LocalDecks.PlayerHeroes.First(id => LocalDecks.IsAllowedInDeck(SabberStoneCore.Model.Cards.FromId(card), SabberStoneCore.Model.Cards.FromId(id).Class));
            var draft = profile.CreateDraft("Pack", hero);
            profile.SaveDraft(draft.Id, draft.Name, hero, new[] { card, "CS2_182" }, draft.SortOrder);
            var saved = profile.GetDraft(draft.Id);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var manager = CollectionManager.Get(); manager.PremiumAware = true; manager.Counts[card] = 1;
            var native = new CollectionDeck(saved); var baseline = new CollectionDeck(saved);
            manager.Working[saved.Id] = native; manager.Baselines[saved.Id] = baseline;
            if (edited) { manager.Edited = native; native.Cards.Remove("CS2_182"); }
            profile.CommitChange(next => { LocalProfile.AddPremium(next, card, 1, 1); return true; });
            NativeUIProbe.AddNativePremiumGrants();
            Check(manager.GetOwnedCount(card, TAG_PREMIUM.GOLDEN) == 1 && profile.GetDraft(saved.Id).CardEntries.All(row => row.Premium == 0)
                && new[] { native, baseline }.All(deck => NativeUIProbe.DeckEntries(deck).Single(row => row.Id == card).Premium == 0),
                "pack premium catch-up credits collection without switching saved/working/baseline decks: " + edited);
            Check(native.GetCardsWithCardID().Contains("CS2_182") == !edited, "pack refresh preserves unsaved removal");
        }
        foreach (bool edited in new[] { false, true })
        {
            using var profile = new LocalProfile(Path.Combine(root, $"mastery-{edited}.xml"));
            const string card = "CATA_213";
            profile.CommitChange(next => { next.Collection.Add(new LocalCardCount { Id = card, Count = 1, Acquired = 1 }); return true; });
            string hero = LocalDecks.PlayerHeroes.First(id => LocalDecks.IsAllowedInDeck(SabberStoneCore.Model.Cards.FromId(card), SabberStoneCore.Model.Cards.FromId(id).Class));
            var draft = profile.CreateDraft("Mastery", hero);
            profile.SaveDraft(draft.Id, draft.Name, hero, new[] { card, "CS2_182" }, draft.SortOrder);
            var saved = profile.GetDraft(draft.Id);
            NativeUIProbe.SetCraftingRecipe(profile, saved, false);
            var manager = CollectionManager.Get(); manager.PremiumAware = true; manager.Counts[card] = 1;
            var native = new CollectionDeck(saved); var baseline = new CollectionDeck(saved);
            manager.Working[saved.Id] = native; manager.Baselines[saved.Id] = baseline;
            if (edited) { manager.Edited = native; native.Cards.Remove("CS2_182"); }
            foreach (int premium in new[] { 1, 3, 2 })
            {
                profile.CommitChange(next => {
                    next.MasteryGrants.Add(new LocalMasteryGrant { CardId = card, Premium = premium, Copies = 1, Wins = 30, ThresholdVersion = 1 });
                    LocalProfile.AddPremium(next, card, premium, 1);
                    LocalCrafting.ReconcileUpgradeDeck(next.CustomDecks.Single(row => row.Id == saved.Id), card, 0, 1, premium);
                    return true;
                });
                NativeUIProbe.AddNativePremiumGrants();
                Check(profile.OwnedCount(card) == 1 && manager.GetOwnedCount(card, (TAG_PREMIUM)premium) == 1
                    && new[] { native, baseline }.All(deck => NativeUIProbe.DeckEntries(deck).Single(row => row.Id == card).Premium == premium),
                    "mastery native delta refreshes working/baseline premium while Normal remains owned: " + (edited, premium));
                Check(native.GetCardsWithCardID().Contains("CS2_182") == !edited, "mastery refresh preserves unsaved removal");
                int refreshes = CollectionDeckTray.Refreshes, rewards = manager.RewardCalls;
                NativeUIProbe.AddNativePremiumGrants();
                Check(CollectionDeckTray.Refreshes == refreshes && manager.RewardCalls == rewards, "mastery native projection is bounded/idempotent");
            }
            // Bootstrap can be missing several earned versions at once; Signature must never downgrade Diamond.
            manager.PremiumCounts.Clear();
            native.CopyFrom(new CollectionDeck(saved)); baseline.CopyFrom(new CollectionDeck(saved));
            if (edited) native.Cards.Remove("CS2_182");
            NativeUIProbe.AddNativePremiumGrants();
            Check(new[] { native, baseline }.All(deck => NativeUIProbe.DeckEntries(deck).Single(row => row.Id == card).Premium == 2),
                "catch-up projects multiple premium deltas in quality order");
        }
        using (var profile = new LocalProfile(Path.Combine(root, "core-rematch.xml")))
        {
            var draft = profile.CreateDraft("Core reprints", "HERO_08");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "CORE_CS2_029", "CS2_029" }, draft.SortOrder);
            var context = profile.AchievementContextFor(new LocalDeck(draft.Name, draft.Hero, new[] { "CORE_CS2_029", "CS2_029" }), true, "npc-mastery", 2500, "Diamond");
            var match = new LocalMatchRecord { Deck = "custom:" + draft.Id, AchievementContext = context };
            Check(profile.PlayedDeckChange(match) == null, "raw-print Core snapshot permits unchanged rematch");
            foreach (var row in context.MainDeck) row.BaseCardId = LocalCardCatalog.CopyGroup(row.BaseCardId);
            context.MainDeck.Reverse();
            Check(profile.PlayedDeckChange(match) == null, "old CopyGroup Core snapshot permits unchanged rematch regardless of row order");
            context.MainDeck[0].Copies++;
            Check(profile.PlayedDeckChange(match) != null, "rematch still rejects changed copy counts");
        }
        } finally { Directory.Delete(root, true); }
#endif
    }
    private static void EditorSave(LocalProfile profile, CollectionDeck native, LocalCustomDeck saved)
    {
        if (profile == null || NativeUIProbe.IsUnrestoredCraftingRecipe(native, saved)) return;
        bool exact = native.GetCardsWithCardID().OrderBy(id => id).SequenceEqual(saved.Cards.OrderBy(id => id))
            && NativeUIProbe.SideboardSignature(native.Rows()) == NativeUIProbe.SideboardSignature(saved.Sideboards);
        if (!exact) profile.SaveDraft(saved.Id, native.Name, native.HeroCardID, native.GetCardsWithCardID(), saved.SortOrder, native.Rows());
    }
}
// Unity-dependent boundaries only. Projection and save-guard decisions above execute production methods.
internal static partial class NativeUIProbe
{
    private static readonly Dictionary<long, string> unrestoredCraftingRecipes = new();
    private static LocalProfile Profile;
    private static readonly HashSet<string> Heroes = new(LocalDecks.PlayerHeroes, StringComparer.Ordinal);
    internal static LocalProfile RewardProfile => Profile;
    private static readonly Dictionary<long, LocalDeck> decks = new();
    internal static void SetCraftingRecipe(LocalProfile profile, LocalCustomDeck saved, bool loaner)
    {
        Profile = profile;
        decks.Clear();
        string name = loaner ? saved.Name.Substring("Loaner: ".Length) : saved.Name;
        decks[saved.Id] = LocalProfile.LedgerDeck(name, saved.Hero, saved.Cards, saved.Sideboards, false);
        CollectionManager.Instance = new();
    }
    internal static string SideboardSignature(IEnumerable<LocalSideboard> rows) => string.Join(";", rows.OrderBy(row => row.Source)
        .Select(row => row.Source + ":" + string.Join(",", row.Cards.OrderBy(id => id))));
}
internal sealed class CollectionDeck
{
    internal CollectionDeck MainDeck;
    internal long ID;
    internal string Name, HeroCardID;
    internal List<string> Cards;
    internal Dictionary<string, CollectionDeck> Sideboards = new();
    internal bool FailProjection;
    internal bool IsBeingEdited() => ReferenceEquals(this, CollectionManager.Get().Edited);
    internal string Contents() => string.Join(";", GetSlotsIncludingSideboards().Select(slot => slot.CardID + ":" + slot.Count + ":" + slot.GetCount(TAG_PREMIUM.GOLDEN) + ":" + slot.NumSubCards)) + "|" + NativeUIProbe.SideboardSignature(Rows());
    internal void CopyFrom(CollectionDeck source)
    {
        ID = source.ID; Name = source.Name; HeroCardID = source.HeroCardID;
        Cards = source.Cards.ToList();
        slots.Clear();
        foreach (var slot in source.GetSlots()) { var copy = new CollectionDeckSlot(); copy.CopyFrom(slot); slots.Add(copy); }
        Sideboards = source.Sideboards.ToDictionary(row => row.Key, row => { var copy = new CollectionDeck(); copy.CopyFrom(row.Value); return copy; });
    }
    internal IEnumerable<CollectionDeckSlot> GetSlotsIncludingSideboards() => GetSlots().Concat(Sideboards.Values.SelectMany(side => side.GetSlots()));
    internal CollectionDeckSlot FindPrimaryFabledCardSlot(EntityDef entity) => GetSlots().FirstOrDefault(slot => slot.CardID == "TIME_005" && entity.CardId.StartsWith("TIME_005") && entity.CardId != "TIME_005");
    internal CollectionDeck() { Cards = new(); }
    internal CollectionDeck(LocalCustomDeck saved)
    {
        ID = saved.Id; Name = saved.Name; HeroCardID = saved.Hero; Cards = saved.Cards.ToList();
        foreach (var row in saved.Sideboards) Sideboards[row.Source] = new() { Cards = row.Cards.ToList() };
    }
    internal List<string> GetCardsWithCardID() => GetSlots().SelectMany(slot => Enumerable.Repeat(slot.CardID, slot.Count)).ToList();
    // Native order within a slot: Diamond, Signature, Golden, Normal.
    internal List<CardWithPremiumStatus> GetCardsWithPremiumStatus() => GetSlots().SelectMany(slot =>
        new[] { TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.NORMAL }.SelectMany(premium =>
            Enumerable.Repeat(new CardWithPremiumStatus(GameUtils.TranslateCardIdToDbId(slot.CardID), premium), slot.GetCount(premium)))).ToList();
    internal void FillFromTemplateDeck(CollectionManager.TemplateDeck template)
    {
        if (FailProjection) throw new IOException("projection failed");
        if (!CollectionManager.Get().PremiumAware)
        {
            Cards = template.m_cardIds.SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value)).ToList();
            return;
        }
        // Native: best owned version first, the rest Normal (ghosts when Normal runs out).
        slots.Clear(); Cards = new();
        foreach (var pair in template.m_cardIds)
        {
            int left = pair.Value;
            foreach (var premium in new[] { TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE, TAG_PREMIUM.GOLDEN })
                for (int take = Math.Min(left, CollectionManager.Get().GetOwnedCount(pair.Key, premium)); take > 0; take--, left--)
                    AddCard(pair.Key, premium, true);
            for (; left > 0; left--) AddCard(pair.Key, TAG_PREMIUM.NORMAL, true);
        }
    }
    internal void ClearSideboards() { if (FailProjection) throw new IOException("projection failed"); Sideboards.Clear(); }
    internal CollectionDeck GetOrCreateSideboard(string id, TAG_PREMIUM premium) => Sideboards[id] = new() { MainDeck = this };
    internal bool AddCard(string id, TAG_PREMIUM premium, bool allowInvalid)
    {
        int used = (MainDeck ?? this).GetSlotsIncludingSideboards().Where(slot => slot.CardID == id && slot.Owned).Sum(slot => slot.GetCount(premium));
        bool owned = CollectionManager.Get().GetOwnedCount(id, premium) > used;
        // Native: a version that is not owned becomes a Normal ghost.
        if (CollectionManager.Get().PremiumAware && !owned) premium = TAG_PREMIUM.NORMAL;
        var slot = GetSlots().FirstOrDefault(slot => slot.CardID == id && slot.Owned == owned)
            ?? InsertSlotWithCard(id, premium, owned, 0);
        slot.SetCount(slot.GetCount(premium) + 1, premium);
        Cards.Add(id);
        return true;
    }
    internal List<CollectionDeckSlot> GetSlots()
    {
        slots.RemoveAll(slot => !Cards.Contains(slot.CardID));
        foreach (string id in Cards.Distinct())
            if (!slots.Any(slot => slot.CardID == id)) { var slot = new CollectionDeckSlot { CardID = id }; slot.SetCount(Cards.Count(card => card == id), TAG_PREMIUM.NORMAL); slots.Add(slot); }
        return slots;
    }
    private readonly List<CollectionDeckSlot> slots = new();
    internal Dictionary<string, CollectionDeck> GetAllSideboards() => Sideboards;
    internal CollectionDeckSlot InsertSlotWithCard(string id, TAG_PREMIUM premium, bool owned, int count)
    {
        var slot = new CollectionDeckSlot { CardID = id, Owned = owned };
        slot.SetCount(count, premium);
        slots.Add(slot);
        return slot;
    }
    internal void InitCardCount() => Cards = slots.SelectMany(slot => Enumerable.Repeat(slot.CardID, slot.Count)).ToList();
    internal IEnumerable<LocalSideboard> Rows() => Sideboards.Select(row => new LocalSideboard { Source = row.Key, Cards = row.Value.Cards.ToList() });
}
internal sealed class CollectionDeckSlot
{
    internal string CardID;
    private Dictionary<TAG_PREMIUM, int> counts = new();
    internal int Count => counts.Values.Sum();
    internal int NumSubCards;
    internal bool Owned = true;
    internal int GetCount(TAG_PREMIUM premium) => counts.GetValueOrDefault(premium);
    internal void SetCount(int count, TAG_PREMIUM premium) => counts[premium] = count;
    internal EntityDef GetEntityDef() => new() { CardId = CardID };
    internal void CopyFrom(CollectionDeckSlot source) { CardID = source.CardID; Owned = source.Owned; counts = new(source.counts); }
}
internal sealed class CollectionManager
{
    internal int RewardCalls;
    internal void AddCardRewards(List<CardRewardData> rewards, bool notify)
    {
        RewardCalls++;
        foreach (var reward in rewards) PremiumCounts[(reward.Id, reward.Premium)] = PremiumCounts.GetValueOrDefault((reward.Id, reward.Premium)) + reward.Count;
    }
    internal CollectionDeck Edited;
    internal bool FailNotification;
    internal CollectionDeck GetEditedDeck() => Edited;
    internal readonly Dictionary<string, int> Counts = new();
    internal readonly Dictionary<(string, TAG_PREMIUM), int> PremiumCounts = new();
    internal bool PremiumAware;
    internal int GetNumCopiesInCollection(string id, TAG_PREMIUM premium) => premium == TAG_PREMIUM.NORMAL
        ? Counts.GetValueOrDefault(id) : PremiumCounts.GetValueOrDefault((id, premium));
    internal int GetOwnedCount(string id, TAG_PREMIUM premium) => GetNumCopiesInCollection(id, premium);
    internal int GetTrialCardCount(string id, TAG_PREMIUM premium) => 0;
    internal sealed class CardModification
    {
        internal enum ModificationType { Add, Remove }
        internal string cardID;
        internal TAG_PREMIUM premium;
        internal int count;
        internal bool seenBefore;
        internal ModificationType modificationType;
    }
    internal bool SawCraftingSynchronize;
    internal void OnCardsModified(List<CardModification> changes)
    {
        SawCraftingSynchronize |= NativeUIProbe.craftingSynchronize;
        foreach (var change in changes)
        {
            Counts[change.cardID] = Counts.GetValueOrDefault(change.cardID) + (change.modificationType == CardModification.ModificationType.Add ? change.count : -change.count);
            foreach (var deck in Working.Values)
                if (change.cardID == "TIME_005") deck.Cards.RemoveAll(id => id != "CS2_182"); else deck.Sideboards.Clear();
        }
        if (FailNotification) throw new IOException("notification failed after reconciliation");
    }
    internal static CollectionManager Instance = new();
    internal static CollectionManager Get() => Instance;
    internal readonly Dictionary<long, CollectionDeck> Working = new(), Baselines = new();
    internal CollectionDeck GetDeck(long id) => Working.GetValueOrDefault(id);
    internal CollectionDeck GetBaseDeck(long id) => Baselines.GetValueOrDefault(id);
    internal sealed class TemplateDeck
    {
        internal string m_title;
        internal RuneType m_rune1, m_rune2, m_rune3;
        internal Dictionary<string, int> m_cardIds = new();
    }
}
internal sealed class CardRewardData
{
    internal readonly string Id;
    internal readonly TAG_PREMIUM Premium;
    internal readonly int Count;
    internal CardRewardData(string id, TAG_PREMIUM premium, int count) { Id = id; Premium = premium; Count = count; }
}
internal static class NativeDeckRunes
{
    internal static RuneType[] Order(string hero, IEnumerable<string> ids) => new RuneType[3];
}
internal enum RuneType { NONE }
internal enum TAG_PREMIUM { NORMAL, GOLDEN, DIAMOND, SIGNATURE }
internal sealed class CardWithPremiumStatus
{
    internal CardWithPremiumStatus(long id, TAG_PREMIUM tag) { cardId = id; premium = tag; }
    internal long cardId { get; }
    internal TAG_PREMIUM premium { get; set; }
}
internal static class GameUtils
{
    private static readonly List<string> known = new();
    internal static int TranslateCardIdToDbId(string id) { int index = known.IndexOf(id); if (index < 0) { known.Add(id); index = known.Count - 1; } return index + 1; }
    internal static string TranslateDbIdToCardId(int id) => known[id - 1];
}
internal static partial class PegasusUtil { internal sealed class Collection { internal List<CardStack> Stacks = new(); } }
internal sealed class CardStack { internal PegasusShared.CardDef CardDef; internal int Count, NumSeen; internal Date LatestInsertDate; }
internal sealed class Date { internal int Year, Month, Day, Hours, Min, Sec; }
namespace PegasusShared { internal sealed class CardDef { internal int Asset, Premium; } }

internal interface ICraftingLedger { IReadOnlyDictionary<string, int> OwnedCounts(); int OwnedCount(string id); }
internal static partial class NativeCrafting
{
    private sealed class EmptyLedger : ICraftingLedger { public IReadOnlyDictionary<string, int> OwnedCounts() => new Dictionary<string, int>(); public int OwnedCount(string id) => 0; }
    internal static void Remove(string id) => Synchronize(new EmptyLedger(), new[] { id });
}
internal sealed class EntityDef { internal string CardId; }
internal sealed class CollectionDeckTray
{
    internal static int Refreshes, IndicatorRefreshes;
    internal static CollectionDeckTray Get() => new();
    internal CollectionDeckTray GetCardsContent() => this;
    internal CollectionDeckTray GetDecksContent() => this;
    internal void RefreshMissingCardIndicators() => IndicatorRefreshes++;
    internal void UpdateCardList() => Refreshes++;
}
