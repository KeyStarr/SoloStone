using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using System.Xml.Serialization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

// #57 welcome gift: first-launch grant, crash safety, Seen, tamper rejection, kit redemption and the kit choice rule.
internal static class WelcomeGiftTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, string root)
    {
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(file)!; }
        void Write(string path, LocalProfileData data) { using var file = File.Create(path); serializer.Serialize(file, data); }
        string Xml(LocalProfileData value) { using var text = new StringWriter(); serializer.Serialize(text, value); return text.ToString(); }
        string dir = Path.Combine(root, "welcome"); Directory.CreateDirectory(dir);
        // One directory per save: a save's backups/ beside a missing file means an earlier save (no gift).
        string NewPath(string name)
        {
            string slot = Path.Combine(dir, name); if (Directory.Exists(slot)) Directory.Delete(slot, true);
            Directory.CreateDirectory(slot); return Path.Combine(slot, "profile.xml");
        }
        // Derive the contract from policy + pinned client data, not the gift or pack-open grant table.
        var productRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (productRoot != null && !File.Exists(Path.Combine(productRoot.FullName, "data/pools/offline-standard-v1.json"))) productRoot = productRoot.Parent;
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(productRoot!.FullName, "data/pools/offline-standard-v1.json")));
        var active = policy.RootElement.GetProperty("metas").EnumerateArray().Single(meta =>
            meta.GetProperty("id").GetString() == policy.RootElement.GetProperty("activeMeta").GetString());
        var extras = active.GetProperty("extraCards").EnumerateArray().Select(id => id.GetString()!).ToHashSet(StringComparer.Ordinal);
        using var file = File.OpenRead(Path.Combine(productRoot.FullName, "data/carddb/37.0.0.254477/cards.json.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var pinned = JsonDocument.Parse(gzip);
        var eventCards = pinned.RootElement.GetProperty("cards").EnumerateArray().Where(card =>
            extras.Contains(card.GetProperty("cardId").GetString()!) && card.GetProperty("cardSet").GetProperty("name").GetString() == "EVENT").ToArray();
        check(eventCards.All(card => card.GetProperty("rarity").GetString() is "EPIC" or "LEGENDARY"), "every active event has a retail free-track rarity");
        var expected = eventCards.SelectMany(card => new[] { 0, 1 }.Select(premium =>
            (Id: card.GetProperty("cardId").GetString()!, Premium: premium, Count: card.GetProperty("rarity").GetString() == "LEGENDARY" ? 1 : 2)))
            .Append((Id: "BE_036", Premium: 0, Count: 1)).OrderBy(row => row.Id, StringComparer.Ordinal).ThenBy(row => row.Premium).ToArray();
        var definitions = LocalWelcomeGift.Cards;
        check(definitions.Select(row => (Id: row.CardId, row.Premium, row.Count)).OrderBy(row => row.Id, StringComparer.Ordinal).ThenBy(row => row.Premium)
            .SequenceEqual(expected), "welcome table equals active-meta EVENT extras at retail free-track amounts plus Normal M.O.T.H.E.R.");
        int Granted(string id, int count) => LocalProfile.KnownLedgerCard(id) ? count : 0;
        bool GiftCards(LocalProfile p) => p.Welcome.Cards.Count == expected.Length && expected.All(row =>
            p.OwnedCount(row.Id, row.Premium) == Granted(row.Id, row.Count) && p.Welcome.Cards.Count(grant => grant.CardId == row.Id
                && grant.Premium == row.Premium && grant.Count == Granted(row.Id, row.Count) && grant.OwnedBefore == 0) == 1);
        bool Gifted(LocalProfile p, int goldBefore = 0) => p.Gold == goldBefore + LocalWelcomeGift.Gold && p.DustBalance == LocalWelcomeGift.Dust
            && p.PackInventory.Single().PackType == FairPackRules.Standard && p.UnopenedPacks == LocalWelcomeGift.Packs
            && p.DeckKits.Count(k => k.Id == LocalWelcomeGift.KitId && k.MilestoneId == LocalWelcomeGift.KitMilestoneId
                && k.GoldCap == LocalWelcomeGift.KitGoldCap && !k.RedeemedPurchaseSequence.HasValue) == 1
            && LocalWelcomeGift.IsKnown(p.Welcome) && !p.Welcome.Seen && GiftCards(p);
        bool Plain(LocalProfile p) => p.Gold == 0 && p.DustBalance == 0 && p.UnopenedPacks == 0 && p.DeckKits.Count == 0
            && p.Welcome == null && !p.WelcomePopupPending;

        check(LocalWelcomeGift.PackType == FairPackRules.Standard && LocalWelcomeGift.Packs == 10 && LocalWelcomeGift.Gold == 500
            && LocalWelcomeGift.Dust == 400 && LocalWelcomeGift.KitGoldCap == 2500 && LocalWelcomeGift.KitId == "kit:welcome"
            && LocalWelcomeGift.KitMilestoneId == "welcome", "owner-approved V1 amounts");

        // Plain construction never grants (unit tests and tools open saves this way).
        string plainPath = NewPath("plain");
        using (var p = new LocalProfile(plainPath)) {
            check(Plain(p) && !File.Exists(plainPath), "a missing file opens in memory at 0/0/0 without writing");
            p.SelectCardBack(0);
            check(!p.GrantWelcomeIfNew() && Plain(p), "an instance that already wrote its file never grants");
        }
        using (var p = new LocalProfile(plainPath)) check(!p.GrantWelcomeIfNew() && Plain(p), "an existing file without a gift is never eligible");

        // First launch: one grant, in the first commit; repeats and reopens never grant again.
        string first = NewPath("first-launch");
        byte[] granted;
        using (var p = new LocalProfile(first)) {
            check(p.GrantWelcomeIfNew() && File.Exists(first) && Gifted(p) && p.WelcomePopupPending, "first launch grants the gift in the first commit");
            check(p.OwnedCount("BE_036") == Granted("BE_036", 1),
                "welcome grants Normal M.O.T.H.E.R. only when admitted by this build");
            string summary = LocalWelcomeGift.CardSummary(p.Welcome);
            int admitted = expected.Where(row => LocalProfile.KnownLedgerCard(row.Id)).Select(row => row.Id).Distinct().Count();
            check(!new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" }.All(LocalCardCatalog.ActiveMetaExtraCards.Contains) || admitted == 22,
                "the regenerated catalog grants all 22 welcome cards");
            check(summary == admitted + " cards (" + expected.Sum(row => Granted(row.Id, row.Count)) + " copies) added to your collection.",
                "welcome summary counts every actual card reward in one scroll line");
            granted = File.ReadAllBytes(first);
            check(!p.GrantWelcomeIfNew() && Gifted(p) && granted.SequenceEqual(File.ReadAllBytes(first)), "a second call is a no-op");
        }
        using (var p = new LocalProfile(first)) {
            check(!p.GrantWelcomeIfNew() && Gifted(p), "restart never re-grants");
            p.SelectCardBack(0);
        }
        check(File.ReadAllBytes(first).SequenceEqual(granted) && Xml(Read(first)).Contains("<Welcome>"), "snapshot round trip keeps the receipt byte-equal");

        GrantOnlyPurchases(check, reject, Read(first), dir);

        // An earlier build recorded these rows as zero before admission; a later catalog never re-grants them.
        string earlierCatalog = NewPath("earlier-catalog");
        var earlierGift = Read(first);
        var waitingIds = new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" };
        foreach (var row in earlierGift.Welcome.Cards.Where(row => waitingIds.Contains(row.CardId))) row.Count = 0;
        earlierGift.Collection.RemoveAll(row => waitingIds.Contains(row.Id));
        earlierGift.PremiumCollection.RemoveAll(row => waitingIds.Contains(row.Id));
        earlierGift.UnseenCards.RemoveAll(row => waitingIds.Contains(row.Id));
        Write(earlierCatalog, earlierGift);
        using (var p = new LocalProfile(earlierCatalog)) check(!p.GrantWelcomeIfNew() && p.MarkWelcomeSeen()
            && waitingIds.All(id => p.OwnedCount(id) == 0 && p.OwnedCount(id, 1) == 0),
            "zero receipts from an earlier catalog load and commit without a retroactive grant");
        using (var p = new LocalProfile(earlierCatalog)) check(p.Welcome.Seen && !p.GrantWelcomeIfNew(),
            "earlier catalog zero receipts survive restart after admission");

        // The creation snapshot may already own cards. Normal and Golden have independent playset caps.
        string capped = NewPath("pre-owned");
        using (var p = new LocalProfile(capped)) {
            var seed = (LocalProfileData)typeof(LocalProfile).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!;
            foreach (var (id, premium, count) in new[] { ("CATA_EVENT_000", 0, 1), ("CATA_EVENT_000", 1, 2),
                ("CATA_EVENT_001", 0, 2), ("CATA_EVENT_001", 1, 1), ("JAIL_EVENT_100", 0, 1),
                ("TIME_EVENT_998", 1, 1), ("BE_036", 0, 1) })
                if (LocalProfile.KnownLedgerCard(id)) (premium == 0 ? seed.Collection : seed.PremiumCollection).Add(new LocalCardCount {
                    Id = id, Premium = premium, Count = count, Acquired = Math.Min(count, 2) });
            check(p.GrantWelcomeIfNew(), "pre-owned creation grants once");
            foreach (var definition in definitions) {
                int before = definition.CardId == "CATA_EVENT_000" ? (definition.Premium == 0 ? 1 : 2)
                    : definition.CardId == "CATA_EVENT_001" ? (definition.Premium == 0 ? 2 : 1)
                    : definition.CardId == "JAIL_EVENT_100" && definition.Premium == 0 || definition.CardId == "TIME_EVENT_998" && definition.Premium == 1
                        || definition.CardId == "BE_036" ? 1 : 0;
                if (!LocalProfile.KnownLedgerCard(definition.CardId)) before = 0;
                var receipt = p.Welcome.Cards.Single(row => row.CardId == definition.CardId && row.Premium == definition.Premium);
                int added = Granted(definition.CardId, Math.Max(0, definition.Count - before));
                check(receipt.OwnedBefore == before && receipt.Count == added && p.OwnedCount(definition.CardId, definition.Premium) == before + added,
                    "independent premium cap and actual receipt: " + definition.CardId + ":" + definition.Premium);
            }
            var detached = p.Welcome; detached.Cards[0].Count = 99;
            check(p.Welcome.Cards[0].Count != 99, "welcome card receipts are detached");
            var actual = p.Welcome.Cards.Where(row => row.Count > 0).ToArray();
            check(LocalWelcomeGift.CardSummary(p.Welcome) == actual.Select(row => row.CardId).Distinct().Count() + " cards ("
                + actual.Sum(row => row.Count) + " copies) added to your collection." && actual.All(row => row.CardId != "BE_036"),
                "summary omits capped zero grants and counts actual additions");
        }
        byte[] cappedBytes = File.ReadAllBytes(capped);
        using (var p = new LocalProfile(capped)) check(!p.GrantWelcomeIfNew() && p.WelcomePopupPending
            && p.OwnedCount("CATA_EVENT_000") == 2 && p.OwnedCount("CATA_EVENT_000", 1) == 2
            && cappedBytes.SequenceEqual(File.ReadAllBytes(capped)), "capped receipt resumes presentation after restart without adding copies");

        // Simulate the next release dropping the V2 event cards. Saved receipts dispatch by their own version.
        var tables = (Dictionary<int, (string Id, int Normal, int Golden)[]>)typeof(LocalWelcomeGift)
            .GetField("tables", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try {
            tables.Add(3, new[] { ("BE_036", 1, 0) });
            check(LocalWelcomeGift.Version == 3 && LocalWelcomeGift.Table(1).Count == 0
                && LocalWelcomeGift.Table(2).Count == 43 && LocalWelcomeGift.Cards.Count == 1
                && LocalWelcomeGift.Table(99) == null, "version lookup keeps historical tables when the current gift changes");
            using (var p = new LocalProfile(first)) {
                check(p.Welcome.Version == 2 && LocalWelcomeGift.IsKnown(p.Welcome) && GiftCards(p)
                    && !p.GrantWelcomeIfNew(), "V2 receipt survives a changed V3 current table without regranting");
                p.SelectCardBack(0);
            }
            check(granted.SequenceEqual(File.ReadAllBytes(first)), "future-version validation preserves V2 save bytes");
            string future = NewPath("gift-v3");
            using (var p = new LocalProfile(future)) check(p.GrantWelcomeIfNew() && p.Welcome.Version == 3
                && p.Welcome.Cards.Count == 1 && p.Welcome.Cards[0].CardId == "BE_036", "new profiles use the current gift version");
            using (var p = new LocalProfile(future)) check(!p.GrantWelcomeIfNew() && p.Welcome.Version == 3, "changed gift persists once across restart");
            var grantOnly = (HashSet<string>)typeof(LocalCrafting).GetField("GrantOnlyIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            check(!LocalWelcomeGift.Cards.Any(row => row.CardId == "CATA_EVENT_000")
                && grantOnly.SetEquals(LocalWelcomeGift.HistoricalCardIds)
                && LocalCrafting.GetIneligibility("CATA_EVENT_000") == CraftIneligibility.GrantOnly,
                "a card present only in the historical V2 table stays GrantOnly");

            // A table ID retired from the DB keeps structural checks, but cannot be checked against a live cap.
            tables[3] = new[] { ("WELCOME_RETIRED_TEST", 2, 0) };
            var retired = Read(future);
            retired.Welcome.Cards = new List<LocalOwnedCardGrant> {
                new LocalOwnedCardGrant { CardId = "WELCOME_RETIRED_TEST", Count = 0, OwnedBefore = 0 } };
            Write(future, retired);
            using (var p = new LocalProfile(future)) check(p.MarkWelcomeSeen(), "unknown historical table ID still loads and commits");
            using (var p = new LocalProfile(future)) check(p.Welcome.Seen && !p.GrantWelcomeIfNew(), "unknown ID receipt survives restart without a grant");
            foreach (int count in new[] { -1, 3 }) {
                retired.Welcome.Cards[0].Count = count; Write(future, retired);
                reject(() => { using var p = new LocalProfile(future); }, "unknown ID still rejects structurally invalid count " + count);
            }
        } finally { tables.Remove(3); }

        // Both directions of rarity correction preserve the historical playset, independently for each premium.
        foreach (var (id, rarity) in new[] { ("CATA_EVENT_000", Rarity.LEGENDARY), ("JAIL_EVENT_100", Rarity.EPIC) }) {
            var card = Cards.FromId(id); int oldRarity = card.Tags[GameTag.RARITY];
            try {
                card.Tags[GameTag.RARITY] = (int)rarity;
                using (var p = new LocalProfile(first)) check(GiftCards(p) && !p.GrantWelcomeIfNew(), "welcome survives rarity flip: " + id);
                using (var p = new LocalProfile(capped)) check(p.WelcomePopupPending, "pre-owned historical caps survive rarity flip: " + id);
                check(granted.SequenceEqual(File.ReadAllBytes(first)), "rarity correction leaves saved receipt unchanged: " + id);
            } finally { card.Tags[GameTag.RARITY] = oldRarity; }
        }

        string allCards = NewPath("all-cards");
        var allCardsOptions = new ProfileOptions { AllCards = true };
        using (var p = new LocalProfile(allCards, allCardsOptions)) {
            p.PersistNewProfile(allCardsOptions);
            check(p.Welcome.Version == LocalWelcomeGift.Version && p.Welcome.Cards.Count == 43
                && definitions.Where(row => row.Premium == 1).All(row => p.OwnedCount(row.CardId, 1) == Granted(row.CardId, row.Count)),
                "AllCards creation keeps Golden welcome copies at the playset cap");
        }
        byte[] allCardsBytes = File.ReadAllBytes(allCards);
        using (var p = new LocalProfile(allCards, allCardsOptions)) check(!p.GrantWelcomeIfNew()
            && definitions.Where(row => row.Premium == 1).All(row => p.OwnedCount(row.CardId, 1) == Granted(row.CardId, row.Count))
            && allCardsBytes.SequenceEqual(File.ReadAllBytes(allCards)), "AllCards receipt validates on restart without adding Golden copies");

        // Old receipts omit Cards, remain V1, and never receive a retroactive gift or new popup after Seen.
        string oldPath = NewPath("gift-v1");
        var old = Read(first); old.Welcome.Version = 1; old.Welcome.Cards.Clear();
        foreach (var definition in definitions)
            (definition.Premium == 0 ? old.Collection : old.PremiumCollection).RemoveAll(row => row.Id == definition.CardId && row.Premium == definition.Premium);
        old.UnseenCards.RemoveAll(row => definitions.Any(definition => definition.CardId == row.Id && definition.Premium == row.Premium));
        Write(oldPath, old);
        check(!File.ReadAllText(oldPath).Contains("<Cards"), "V1 welcome receipt omits the new field");
        using (var p = new LocalProfile(oldPath)) {
            check(LocalWelcomeGift.IsKnown(p.Welcome) && p.Welcome.Version == 1 && p.Welcome.Cards.Count == 0 && p.WelcomePopupPending && !p.GrantWelcomeIfNew()
                && p.OwnedCount("BE_036") == 0, "V1 receipt still validates without regranting");
            check(LocalWelcomeGift.CardSummary(p.Welcome) == "", "V1 presentation names no cards");
            p.MarkWelcomeSeen();
        }
        using (var p = new LocalProfile(oldPath)) check(LocalWelcomeGift.IsKnown(p.Welcome) && p.Welcome.Version == 1 && !p.WelcomePopupPending && !p.GrantWelcomeIfNew(), "V1 Seen persists");

        // Crash before or at the first commit: no save; the retry (same or next process) grants exactly once.
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            string crash = NewPath("crash-" + stage);
            using (var p = new LocalProfile(crash)) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                reject(() => p.GrantWelcomeIfNew(), "injected first-commit failure " + stage);
                check(!File.Exists(crash) && !File.Exists(crash + ".tmp") && Plain(p), "failed creation leaves no save and no gift in memory: " + stage);
                p.CommitFailure = null;
                check(p.GrantWelcomeIfNew() && Gifted(p) && !p.GrantWelcomeIfNew(), "retry in the same process grants once: " + stage);
            }
            string next = NewPath("crash-next-" + stage);
            using (var p = new LocalProfile(next)) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                reject(() => p.GrantWelcomeIfNew(), "injected failure before restart " + stage);
            }
            using (var p = new LocalProfile(next)) check(p.GrantWelcomeIfNew() && Gifted(p), "the next launch creates the save with the gift once: " + stage);
            using (var p = new LocalProfile(next)) check(!p.GrantWelcomeIfNew() && Gifted(p), "and never again: " + stage);
        }

        // Leftovers of an earlier save beside a missing profile.xml: not a new player. No gift, no commit (review 2026-10-28).
        foreach (string leftover in new[] { ".tmp", ".restore.tmp", ".v13.bak", ".v13.bak.tmp", "backups" })
        {
            string slot = Path.Combine(dir, "leftover" + leftover.Replace('.', '-')); Directory.CreateDirectory(slot);
            string profilePath = Path.Combine(slot, "profile.xml"), trace = leftover == "backups" ? Path.Combine(slot, "backups", "x.xml") : profilePath + leftover;
            Directory.CreateDirectory(Path.GetDirectoryName(trace)!); File.WriteAllText(trace, "earlier save bytes");
            using (var p = new LocalProfile(profilePath))
                check(!p.GrantWelcomeIfNew() && p.WelcomeSkipped != null && !File.Exists(profilePath) && Plain(p)
                    && File.ReadAllText(trace) == "earlier save bytes", "earlier-save leftover blocks the gift and any commit: " + leftover);
        }
        string emptyBackups = Path.Combine(dir, "leftover-empty-backups"); Directory.CreateDirectory(Path.Combine(emptyBackups, "backups"));
        using (var p = new LocalProfile(Path.Combine(emptyBackups, "profile.xml")))
            check(p.GrantWelcomeIfNew() && p.WelcomeSkipped == null && Gifted(p), "an empty backups directory is not an earlier save");

        // Seen: separate idempotent commit; a failed Seen only shows the popup again.
        using (var p = new LocalProfile(first)) {
            p.CommitFailure = point => { if (point == "replace") throw new IOException("injected seen"); };
            reject(() => p.MarkWelcomeSeen(), "failed Seen commit");
            check(p.WelcomePopupPending && File.ReadAllBytes(first).SequenceEqual(granted), "failed Seen keeps the popup pending and the file unchanged");
            p.CommitFailure = null;
            check(p.MarkWelcomeSeen() && !p.WelcomePopupPending && p.Welcome.Seen, "OK commits Seen");
            byte[] seen = File.ReadAllBytes(first);
            check(!p.MarkWelcomeSeen() && seen.SequenceEqual(File.ReadAllBytes(first)), "Seen is idempotent");
        }
        using (var p = new LocalProfile(first)) check(p.Welcome.Seen && !p.WelcomePopupPending && !p.GrantWelcomeIfNew()
            && p.Gold == LocalWelcomeGift.Gold && p.UnopenedPacks == LocalWelcomeGift.Packs && GiftCards(p), "Seen persists; the gift stays as granted");
        using (var p = new LocalProfile(plainPath)) reject(() => p.MarkWelcomeSeen(), "a save without a gift has nothing to acknowledge");

        // Existing V14 fixture: loads with no receipt and is never eligible.
        string fixture = Path.Combine(AppContext.BaseDirectory, "profile-fixtures", "v14-mono-migrated.xml");
        string fixtureCopy = NewPath("fixture-v14"); File.Copy(fixture, fixtureCopy);
        using (var p = new LocalProfile(fixtureCopy)) check(p.Welcome == null && !p.WelcomePopupPending && !p.GrantWelcomeIfNew()
            && p.DeckKits.All(k => k.Id != LocalWelcomeGift.KitId), "existing V14 fixture: no gift, no popup");
        check(!File.ReadAllText(fixtureCopy).Contains("<Welcome>"), "existing saves keep no Welcome element");
        check(!File.ReadAllText(plainPath).Contains("<Welcome"), "a save without the gift serializes no Welcome element");

        // Tampered receipts are rejected on load.
        var saved = Read(first);
        var tampers = new (string Name, Action<LocalProfileData> Change)[] {
            ("version", d => d.Welcome.Version = 3), ("pack type", d => d.Welcome.PackType = "CATACLYSM"), ("packs", d => d.Welcome.Packs = 11),
            ("gold", d => d.Welcome.Gold = 5000), ("dust", d => d.Welcome.Dust = 0), ("kit id", d => d.Welcome.KitId = "kit:other"),
            ("kit cap", d => d.Welcome.KitGoldCap = 9999), ("kit missing", d => d.DeckKits.RemoveAll(k => k.Id == LocalWelcomeGift.KitId)),
            ("saved kit cap", d => d.DeckKits.Single(k => k.Id == LocalWelcomeGift.KitId).GoldCap = 9999),
            ("saved kit milestone", d => d.DeckKits.Single(k => k.Id == LocalWelcomeGift.KitId).MilestoneId = "floor:gold-10"),
            ("second welcome kit", d => d.DeckKits.Add(new LocalDeckKit { Id = "kit:welcome-2", MilestoneId = LocalWelcomeGift.KitMilestoneId, GoldCap = 2500 })),
            ("orphan kit", d => d.Welcome = null),
            ("orphan milestone kit", d => { d.Welcome = null; d.DeckKits.Single().Id = "kit:other"; }),
            ("card missing", d => d.Welcome.Cards.RemoveAt(0)),
            ("card duplicate", d => d.Welcome.Cards[1] = d.Welcome.Cards[0]),
            ("card null", d => d.Welcome.Cards[0] = null!),
            ("card id", d => d.Welcome.Cards[0].CardId = "BE_EVENT_103"),
            ("card premium", d => d.Welcome.Cards[0].Premium = 3),
            ("card count", d => d.Welcome.Cards[0].Count = 99),
            ("impossible cap", d => d.Welcome.Cards[0].Count = 0),
            ("negative card count", d => d.Welcome.Cards[0].Count = -1),
            ("negative ownership", d => d.Welcome.Cards[0].OwnedBefore = -1),
            ("v1 with cards", d => d.Welcome.Version = 1),
        };
        foreach (var (name, change) in tampers) {
            var bad = LocalProfile.Clone(saved); change(bad);
            string badPath = NewPath("tamper"); Write(badPath, bad);
            reject(() => { using var p = new LocalProfile(badPath); }, "tampered welcome receipt rejected: " + name);
        }
        var noGift = Read(plainPath);
        noGift.DeckKits.Add(new LocalDeckKit { Id = LocalWelcomeGift.KitId, MilestoneId = LocalWelcomeGift.KitMilestoneId, GoldCap = LocalWelcomeGift.KitGoldCap });
        string orphan = NewPath("orphan"); Write(orphan, noGift);
        reject(() => { using var p = new LocalProfile(orphan); }, "kit:welcome without a Welcome receipt rejected");

        // Kit redemption through the ledger: pre-kit quote, after-kit gold, consumed once, replay idempotent.
        using (var p = new LocalProfile(first)) {
            var decks = LocalShop.Default.Products.Where(d => d.Kind == "Deck" && !d.verify && !LocalShop.Default.Withheld(d) && p.PurchasePrice(d.Id) > 0).OrderBy(d => p.PurchasePrice(d.Id)).ToList();
            var cheap = decks.First(); int price = p.PurchasePrice(cheap.Id), due = Math.Max(0, price - LocalWelcomeGift.KitGoldCap);
            check(due <= p.Gold, "cheapest ready-made deck affordable with the welcome kit (" + price + ")");
            check(p.HasUnredeemedDeckKit, "kit badge condition: on while the welcome kit is unredeemed");
            var dear = decks.Last(); int dearPrice = p.PurchasePrice(dear.Id);
            check(dearPrice - LocalWelcomeGift.KitGoldCap > p.Gold, "the dearest deck stays out of reach (" + dearPrice + ")");
            byte[] before = File.ReadAllBytes(first);
            reject(() => p.Purchase(dear.Id, 1, dearPrice, "kit-short", LocalWelcomeGift.KitId), "kit purchase short of gold refused");
            reject(() => p.Purchase("pack-standard", 1, p.PurchasePrice("pack-standard"), "kit-pack", LocalWelcomeGift.KitId), "kits buy ready-made decks only");
            reject(() => p.Purchase(cheap.Id, 1, due, "kit-after", LocalWelcomeGift.KitId), "expected price is the pre-kit quote");
            check(before.SequenceEqual(File.ReadAllBytes(first)) && p.Gold == LocalWelcomeGift.Gold, "refusals change nothing");
            int gold = p.Gold;
            var receipt = p.Purchase(cheap.Id, 1, price, "kit-buy", LocalWelcomeGift.KitId);
            check(!p.HasUnredeemedDeckKit, "kit badge condition: off once the welcome kit is used");
            check(receipt.KitId == LocalWelcomeGift.KitId && receipt.PriceBeforeKit == price && receipt.GoldPaid == due && p.Gold == gold - due
                && p.DeckKits.Single().RedeemedPurchaseSequence == receipt.Sequence && p.DeckKitFor(price) == null, "kit pays up to 2,500 gold and is consumed");
            var replay = p.Purchase(cheap.Id, 1, price, "kit-buy", LocalWelcomeGift.KitId);
            check(replay.Sequence == receipt.Sequence && p.Gold == gold - due, "replay is idempotent");
            reject(() => p.Purchase(cheap.Id, 1, price, "kit-buy", null), "replay with a different kit conflicts");
            var other = decks[1];
            reject(() => p.Purchase(other.Id, 1, p.PurchasePrice(other.Id), "kit-again", LocalWelcomeGift.KitId), "a redeemed kit cannot be used again");
        }
        using (var p = new LocalProfile(first)) check(p.DeckKits.Single().RedeemedPurchaseSequence.HasValue && p.Welcome != null, "redeemed welcome kit reloads valid");

        // Kit choice: the smallest cap that covers the price, else the largest; redeemed kits never offered.
        LocalDeckKit Kit(string id, int cap, long? redeemed = null) => new LocalDeckKit { Id = id, MilestoneId = id, GoldCap = cap, RedeemedPurchaseSequence = redeemed };
        var kits = new[] { Kit("kit:welcome", 2500), Kit("kit:floor:gold-10", 3200), Kit("kit:used", 9000, 4) };
        check(LocalProfile.ChooseDeckKit(kits, 2000).Id == "kit:welcome" && LocalProfile.ChooseDeckKit(kits, 2500).Id == "kit:welcome"
            && LocalProfile.ChooseDeckKit(kits, 2501).Id == "kit:floor:gold-10" && LocalProfile.ChooseDeckKit(kits, 9000).Id == "kit:floor:gold-10"
            && LocalProfile.ChooseDeckKit(new[] { Kit("kit:b", 100), Kit("kit:a", 100) }, 500).Id == "kit:a"
            && LocalProfile.ChooseDeckKit(new[] { Kit("kit:used", 9000, 4) }, 100) == null && LocalProfile.ChooseDeckKit(new LocalDeckKit[0], 1) == null,
            "kit choice: smallest covering cap, else the largest; ties by id; redeemed skipped");
    }
    // Other shop suites start with the grant-only copies a normal welcome save owns.
    internal static void OwnGrantOnly(LocalProfileData data)
    {
        foreach (var row in LocalWelcomeGift.Cards.Where(row => row.Premium == 0 && LocalProfile.KnownLedgerCard(row.CardId))) {
            if (data.Collection.Any(card => card.Id == row.CardId)) continue;
            data.Collection.Add(new LocalCardCount { Id = row.CardId, Count = row.Count, Acquired = row.Count });
        }
    }

    private static void GrantOnlyPurchases(Action<bool, string> check, Action<Action, string> reject,
        LocalProfileData welcome, string root)
    {
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        var grantOnly = LocalWelcomeGift.HistoricalCardIds.ToHashSet(StringComparer.Ordinal);
        string path = Path.Combine(root, "grant-only-purchase.xml");
        void Write(LocalProfileData data) { using var file = File.Create(path); serializer.Serialize(file, data); }
        var legacy = LocalProfile.Clone(welcome);
        legacy.GoldBalance = 100000;
        legacy.Welcome.Version = 1; legacy.Welcome.Cards.Clear();
        legacy.Collection.RemoveAll(row => grantOnly.Contains(row.Id));
        legacy.PremiumCollection.RemoveAll(row => grantOnly.Contains(row.Id));
        legacy.UnseenCards.RemoveAll(row => grantOnly.Contains(row.Id));
        Write(legacy);
        LocalShopProduct[] blocked;
        using (var profile = new LocalProfile(path))
            blocked = LocalShop.Default.Products.Where(product => product.Kind == "Deck" && !LocalShop.Default.Withheld(product)
                && profile.QuoteMissingCopies(product.RecipeId).Cards.Any(row => row.Missing > 0 && grantOnly.Contains(row.Id))).ToArray();
        check(blocked.Length > 0, "real recipes exercise missing event cards");
        foreach (var product in blocked) {
            Write(legacy);
            byte[] before = File.ReadAllBytes(path);
            int refusedPrice = 0;
            for (int restart = 0; restart < 2; restart++) {
                using var profile = new LocalProfile(path);
                refusedPrice = profile.PurchasePrice(product.Id);
                NativeBundleHarness.CheckGrantOnly(check, profile, product);
                check(NativeShopPurchase.Blocker(profile, product) == LocalShop.GrantOnlyDeckMessage,
                    "native purchase state carries the grant-only reason");
                var row = ((MiniJSON.JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<MiniJSON.JsonNode>()
                    .Single(row => (string)row["id"] == product.Id);
                check((string)row["reason"] == LocalShop.GrantOnlyDeckMessage && !(bool)row["purchasable"] && !(bool)row["owned"],
                    "test-control catalog exposes refusal reason and unavailable state");
                profile.CommitFailure = _ => throw new IOException("refused purchase attempted a write");
                foreach (string kit in new[] { (string)null!, LocalWelcomeGift.KitId }) {
                    string reason = null!;
                    try { profile.Purchase(product.Id, 1, profile.PurchasePrice(product.Id), "event-refused", kit); }
                    catch (InvalidOperationException error) { reason = error.Message; }
                    check(reason != null && reason.Contains("welcome gift") && reason.Contains("can't be bought"),
                        product.Id + ": missing grant-only cards refuse purchase with actionable reason, including kit and restart");
                    check(profile.Gold == legacy.GoldBalance && profile.PurchaseSequence == 0 && profile.Purchases.Count == 0
                        && profile.CustomDeckCount == 0 && profile.HasUnredeemedDeckKit
                        && File.ReadAllBytes(path).SequenceEqual(before), "grant-only refusal preserves wallet, collection, kit and receipts");
                }
            }
            foreach (bool goldenOnly in new[] { false, true }) {
                var owned = LocalProfile.Clone(goldenOnly ? legacy : welcome);
                owned.GoldBalance = 100000;
                if (goldenOnly) {
                    owned.PremiumCollection = welcome.PremiumCollection.Select(LocalProfile.Clone).ToList();
                    // M.O.T.H.E.R.'s welcome grant is Normal only.
                    owned.Collection.AddRange(welcome.Collection.Where(row => row.Id == "BE_036").Select(LocalProfile.Clone));
                }
                Write(owned);
                int price;
                using (var profile = new LocalProfile(path)) {
                    price = profile.PurchasePrice(product.Id);
                    check(price == refusedPrice, "unowned grant-only cards add no per-rarity cost to the quote");
                    check(NativeShopPurchase.Blocker(profile, product) == null, "owned event copies enable deck purchase in either premium");
                    var receipt = profile.Purchase(product.Id, 1, price, "event-refused", LocalWelcomeGift.KitId);
                    check(receipt.Granted.Cards.All(row => !grantOnly.Contains(row.CardId)) && profile.IsDeckComplete(receipt.Granted.DeckId.Value)
                        && receipt.GoldPaid == Math.Max(0, price - 2500), "owned grant-only deck buys normally without selling event copies");
                }
                byte[] committed = File.ReadAllBytes(path);
                using (var profile = new LocalProfile(path)) {
                    var replay = profile.Purchase(product.Id, 1, price, "event-refused", LocalWelcomeGift.KitId);
                    check(profile.PurchaseSequence == 1 && profile.CustomDeckCount == 1 && replay.Sequence == 1
                        && !profile.HasUnredeemedDeckKit && committed.SequenceEqual(File.ReadAllBytes(path)), "kit purchase and replay survive restart exactly once");
                }
            }
        }
        // A failed commit with an eligible deck preserves the entire kit transaction; restart can retry it once.
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            var owned = LocalProfile.Clone(welcome); owned.GoldBalance = 100000; Write(owned);
            byte[] before = File.ReadAllBytes(path); var product = blocked[0]; int price;
            using (var profile = new LocalProfile(path)) {
                price = profile.PurchasePrice(product.Id);
                profile.CommitFailure = point => { if (point == stage) throw new IOException("grant-only test " + stage); };
                reject(() => profile.Purchase(product.Id, 1, price, "event-fault", LocalWelcomeGift.KitId), "eligible deck commit failure " + stage);
                check(profile.Gold == 100000 && profile.PurchaseSequence == 0 && profile.CustomDeckCount == 0
                    && profile.HasUnredeemedDeckKit && before.SequenceEqual(File.ReadAllBytes(path)), "failed kit transaction changes no state " + stage);
            }
            using (var profile = new LocalProfile(path)) {
                var receipt = profile.Purchase(product.Id, 1, price, "event-fault", LocalWelcomeGift.KitId);
                check(receipt.Sequence == 1 && profile.CustomDeckCount == 1 && !profile.HasUnredeemedDeckKit,
                    "restart retries failed eligible purchase exactly once " + stage);
            }
        }
        Console.WriteLine("PASS grant-only deck purchases: refusal, quotes, native reason, premiums, kits, commit faults and restart");
    }

}
