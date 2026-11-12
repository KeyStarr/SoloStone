using System.Xml.Serialization;
using SabberStoneCore.Model;

internal static class ReviewRegressions
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action) {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Invalid deck accepted");
    }
    static LocalProfileData Read(string path) {
        using var stream = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
    }
    static void Write(string path, LocalProfileData data) {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
    }
    internal static void Run(string root)
    {
        var failures = new List<string>();
        void Test(string name, Action action) {
            try { action(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); }
        }
        bool source = LocalCoreProgression.AvailableCards.Count != 0;
        Test("counterpart copy limits and preserved invalid drafts", () => {
            Check(NativeUIProjection.DeckCopyCount(new[] { "CS2_029", "CORE_CS2_029" }, "CS2_029") == 2
                && NativeUIProjection.DeckCopyCount(new[] { "CS2_029", "CORE_CS2_029" }, "CORE_CS2_029") == 2,
                "native editor counts printings separately");
            Check(NativeUIProjection.DeckCopyCount(new[] { "EX1_559", "CORE_EX1_559" }, "EX1_559") == 2,
                "native editor misses legendary counterpart");
            Check(!LocalDecks.ValidateStoredDraft("Mixed", "HERO_08", new[] { "CS2_029", "CS2_029", "CORE_CS2_029" }),
                "stored validation ignored combined cap");
            if (!source) return;
            string path = Path.Combine(root, "group-limits.xml");
            long id;
            using (var profile = new LocalProfile(path)) id = profile.CreateDraft("Mixed", "HERO_08").Id;
            var saved = Read(path);
            saved.ClassXp.ForEach(entry => entry.Xp = 2700);
            saved.Collection.Add(new LocalCardCount { Id = "EX1_559", Count = 1, Acquired = 1 });
            Write(path, saved);
            using (var profile = new LocalProfile(path)) {
                profile.SaveDraft(id, "Mixed", "HERO_08", new[] { "CS2_029", "CORE_CS2_029" }, 0);
                Reject(() => profile.SaveDraft(id, "Mixed", "HERO_08", new[] { "CS2_029", "CS2_029", "CORE_CS2_029", "CORE_CS2_029" }, 0));
                Reject(() => profile.SaveDraft(id, "Mixed", "HERO_08", new[] { "EX1_559", "CORE_EX1_559" }, 0));
            }
            saved = Read(path);
            var cards = LocalDecks.Get("mage").CardIds;
            cards.RemoveRange(0, 2); cards.AddRange(new[] { "CORE_CS2_029", "CORE_CS2_029" });
            saved.CustomDecks[0].Cards = cards;
            Write(path, saved);
            using (var profile = new LocalProfile(path)) {
                var draft = profile.CustomDecks.Single();
                // Bootstrap uses Draft, so it must remain projectable for editing.
                LocalDecks.Draft(draft.Name, draft.Hero, draft.Cards);
                Check(!NativeUIProjection.ValidateDraftProjection(id, draft.Name, draft.Hero, cards.ToArray(),
                    draft.Name, draft.Hero, cards.ToArray(), profile.Collection), "old mixed draft marked playable");
                Reject(() => NativeUIProjection.ValidateCustomSelection(id, draft.Hero, cards.ToArray(),
                    draft.Hero, cards.ToArray(), profile.Collection.ToDictionary(pair => pair.Key, pair => pair.Value)));
            }
            Check(Read(path).CustomDecks[0].Cards.SequenceEqual(cards), "load rewrote over-limit draft");
            // Existing mixed legendary printings also load without invalidating the profile.
            saved = Read(path);
            cards = LocalDecks.Get("mage").CardIds;
            cards.RemoveRange(0, 2); cards.AddRange(new[] { "EX1_559", "CORE_EX1_559" });
            saved.CustomDecks[0].Cards = cards;
            Write(path, saved);
            using (var profile = new LocalProfile(path)) {
                var draft = profile.CustomDecks.Single();
                Check(!NativeUIProjection.ValidateDraftProjection(id, draft.Name, draft.Hero, cards.ToArray(),
                    draft.Name, draft.Hero, cards.ToArray(), profile.Collection), "mixed legendary marked playable");
                Reject(() => LocalDecks.Custom(draft.Name, draft.Hero, draft.Cards));
            }
        });
        Test("schedule changes preserve earned Core ownership", () => {
            string path = Path.Combine(root, "schedule.xml");
            int index = Array.FindIndex(LocalCoreProgression.Unlocks, card => card.Hero == "HERO_08" && card.Threshold == 1);
            var old = LocalCoreProgression.Unlocks[index];
            long id;
            using (var profile = new LocalProfile(path)) {
                id = profile.CreateDraft("Earned", "HERO_08").Id;
                if (source) profile.SaveDraft(id, "Earned", "HERO_08", new[] { old.Id }, 0);
            }
            Check(Read(path).CoreScheduleVersion == LocalCoreProgression.ScheduleVersion
                && Read(path).CoreGrants.Single(card => card.Id == old.Id).Count == old.Copies,
                "profile did not persist versioned grants");
            LocalCoreProgression.Unlocks[index] = new CoreUnlock(old.Id, old.Name, old.Hero, 10, old.Copies, old.CopyGroup);
            try {
                using (var profile = new LocalProfile(path)) {
                    Check(!source || profile.Collection[old.Id] == old.Copies, "schedule revoked earned Core card");
                    profile.CreateDraft("Still usable", "HERO_08");
                }
                using var restarted = new LocalProfile(path);
                Check(!source || restarted.Collection[old.Id] == old.Copies, "schedule change revoked grant after commit/restart");
            } finally { LocalCoreProgression.Unlocks[index] = old; }
        });
        Test("non-prefixed Core grants survive validation and draft ownership, never packs", () => {
            const string cardId = "CS3_038";
            string path = Path.Combine(root, "core-original.xml");
            long id;
            using (var profile = new LocalProfile(path)) id = profile.CreateDraft("Core original", "HERO_08").Id;
            var saved = Read(path);
            saved.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
            Write(path, saved);
            using (var profile = new LocalProfile(path)) {
                Check(Read(path).CoreGrants.Single(card => card.Id == cardId).Count == 2,
                    "non-prefixed Core unlock was not granted");
                Check(LocalCardCatalog.IsCore(cardId) && LocalCardCatalog.IsCore("CS3_025")
                    && LocalCardCatalog.CopyGroup(cardId) == cardId, "Core original classification/copy group");
                Check(profile.Collection.ContainsKey(cardId) == source, "Core original engine availability");
                if (source) profile.SaveDraft(id, "Core original", "HERO_08", new[] { cardId, cardId }, 0);
            }
            saved = Read(path);
            Check(saved.Collection.All(card => !LocalCardCatalog.IsCore(card.Id)), "Core original stored as Classic ownership");
            saved.CustomDecks[0].Cards = new() { cardId, cardId };
            Write(path, saved);
            Check(LocalDecks.ValidateStoredDraft("Core original", "HERO_08", saved.CustomDecks[0].Cards),
                "Core original treated as Classic in stored draft validation");
            using (var profile = new LocalProfile(path)) {
                Check(profile.CustomDecks.Count == (source ? 1 : 0), "Core original draft ownership after restart");
                Check(NativeUIProjection.ForcesCorePlayability(true, cardId) == source
                    && !NativeUIProjection.ForcesCorePlayability(false, cardId), "Core original native playability");
            }
            foreach (int version in new[] { 1, LocalCardCatalog.CurrentPoolVersion }) {
                Check(LocalCardCatalog.PackPool(version).All(card => !LocalCardCatalog.IsCore(card.Id)), "Core entered pack pool");
                saved.LastPack = new LocalPackResult { PoolVersion = version,
                    Cards = Enumerable.Range(0, 5).Select(_ => new LocalPackCard { Id = cardId }).ToList() };
                Write(path, saved);
                Reject(() => { using var profile = new LocalProfile(path); });
            }
        });
        Test("hidden drafts do not block load, reorder or visible limit", () => {
            string path = Path.Combine(root, "hidden.xml");
            using (var profile = new LocalProfile(path)) {
                profile.CreateDraft("Visible A", "HERO_08");
                profile.CreateDraft("Hidden A", "HERO_08");
                profile.CreateDraft("Hidden B", "HERO_08");
                profile.CreateDraft("Visible B", "HERO_08");
            }
            var saved = Read(path);
            long a = saved.CustomDecks[0].Id, b = saved.CustomDecks[3].Id;
            saved.CoreScheduleVersion = 42;
            saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_FUTURE", Count = 2 });
            saved.CustomDecks[1].Cards.Add("CORE_UNKNOWN_FUTURE");
            saved.CustomDecks[2].Cards.Add("CORE_UNKNOWN_FUTURE_B"); // unavailable metadata stays hidden; locked known cards remain visible
            Write(path, saved);
            using (var profile = new LocalProfile(path)) {
                Check(profile.CustomDecks.Count == 2, "hidden drafts shown");
                profile.ReorderDrafts(new[] { b, a });
                Reject(() => profile.ReorderDrafts(new[] { b, b }));
                for (int n = 2; n < LocalProfile.MaximumCustomDecks; n++) profile.CreateDraft("Visible " + n, "HERO_08");
                Reject(() => profile.CreateDraft("Overflow", "HERO_08"));
                Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks, "visible count differs from UI limit");
            }
            using (var profile = new LocalProfile(path)) Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks, "restart rejects hidden extras");
            Check(Read(path).CoreScheduleVersion == 42
                && Read(path).CoreGrants.Single(card => card.Id == "CORE_UNKNOWN_FUTURE").Count == 2,
                "downgrade discarded future schedule version/grant");
            var ordered = Read(path).CustomDecks.OrderBy(deck => deck.SortOrder).ThenBy(deck => deck.Id).ToArray();
            Check(ordered.Take(4).Select(deck => deck.Id).SequenceEqual(new[] { b, saved.CustomDecks[1].Id, saved.CustomDecks[2].Id, a }),
                "reorder moved hidden drafts relative to each other or their slots");
        });
        Test("ack commits before refresh, failures do not gate matches", () => {
            string path = Path.Combine(root, "ack-refresh.xml");
            using var profile = new LocalProfile(path);
            var match = profile.Begin("mage", "priest", 7);
            profile.Settle(match.Id, "Won", 5, false);
            Directory.CreateDirectory(path + ".tmp");
            int refreshes = 0;
            bool rejected = false;
            try { NativeUIProjection.AcknowledgeAndRefreshCore(profile, match.Id, () => refreshes++, out _); }
            catch (IOException) { rejected = true; }
            catch (UnauthorizedAccessException) { rejected = true; }
            Check(rejected && refreshes == 0 && profile.PendingRewardPresentation != null, "refresh ran before failed commit");
            Directory.Delete(path + ".tmp");
            bool sawCommitted = false;
            Check(NativeUIProjection.AcknowledgeAndRefreshCore(profile, match.Id, () => {
                sawCommitted = Read(path).PendingRewardPresentationId == null;
                throw new InvalidOperationException("native unavailable");
            }, out var error), "ack was not committed");
            Check(sawCommitted && error?.Message == "native unavailable" && profile.PendingRewardPresentation == null,
                "refresh failure lost acknowledgement or error");
            Check(!NativeUIProjection.AcknowledgeAndRefreshCore(profile, match.Id, () => refreshes++, out error)
                && error == null, "retry must remain acknowledged");
            Check(profile.Begin("mage", "priest", 8) != null, "refresh failure blocked match start");
        });
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
    }
}
