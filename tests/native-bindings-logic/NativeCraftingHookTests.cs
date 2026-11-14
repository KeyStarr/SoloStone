using System.Reflection;

// Run extracted production hooks; substitute only the ledger and native UI boundary.
internal static partial class NativeCrafting
{
    private static readonly TestLog log = new();
    internal static readonly Dictionary<string, int> Attempts = new();
    internal static readonly HashSet<string> Failures = new();
    internal static readonly List<string> Popups = new();
    internal static long PublishedDust;
    private static Action<object> publish = value => {
        Boundary("dust");
        PublishedDust = ((NetCache.NetCacheArcaneDustBalance)value).Balance;
    };
    private static MethodInfo Required(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
    internal static void Boundary(string step)
    {
        Attempts[step] = Attempts.GetValueOrDefault(step) + 1;
        if (Failures.Contains(step)) throw new Exception(step + " failure");
    }
    internal static void NotifyUnblocked(bool blocked) => MassUnblocked(blocked);
    internal static Network.MassDisenchantResponse ReadMassResponse()
    {
        Network.MassDisenchantResponse result = null;
        MassResponse(ref result);
        return result;
    }
    internal static void NotifyMassListeners(CollectionManager manager)
    {
        // Invoke new response prefix when present, otherwise simulate the native dispatcher.
        var prefix = typeof(NativeCrafting).GetMethod("MassCompleted", BindingFlags.NonPublic | BindingFlags.Static);
        if (prefix != null && !(bool)prefix.Invoke(null, new object[] { manager })) return;
        int amount = ReadMassResponse().Amount;
        foreach (var listener in manager.m_massDisenchantListeners.ToArray()) listener(amount);
    }

    // CraftingUI.UpdateCraftingButtonsAndSoulboundText, owned-copy branch (logged in).
    private static string NativeSoulbound(CardSetDbfRecord record, Func<CardSetDbfRecord, bool> featured, Func<CardSetDbfRecord, bool> core) =>
        featured(record) ? "GLUE_CRAFTING_SOULBOUND_FEATURED_DESC" : core(record) ? "GLUE_CRAFTING_SOULBOUND_CORE_DESC" : "GLUE_CRAFTING_SOULBOUND_DESC";
    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; } catch (T) { return true; }
    }

    private static FakeCraftingLedger ResetHooks()
    {
        var ledger = new FakeCraftingLedger();
        CraftingLedgerRegistry.Current = ledger;
        NativeUIProbe.Active = true;
        batch = null; closing = false; massResponse = null; massCompleting = massListenersFailed = massRefreshErrorShown = false;
        Attempts.Clear(); Failures.Clear(); Popups.Clear(); log.Errors.Clear(); PublishedDust = -1;
        NativeUIProbe.RewardProfile = new LocalProfile();
        CraftingManager.Instance = new();
        CollectionManager.Instance = new();
        MassDisenchant.Instance = new();
        return ledger;
    }

    internal static void RunHookTests()
    {
        foreach (string id in new[] { "CATA_EVENT_000", "BE_036" })
        foreach (var premium in new[] { TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE }) {
            var gifts = ResetHooks();
            gifts.Counts[id] = 5;
            NativeUIProbe.RewardProfile.Premium[(id, (int)premium)] = 5;
            var record = new CardValueDbfRecord(); NetCache.CardValue value = new();
            bool allowed = true, future = true, eventActive = true;
            var craft = CraftingManager.CanCraftCardResult.TooManyCopies;
            Assert(!Price(id, premium, ref record, ref value) && record == null
                && (premium == TAG_PREMIUM.NORMAL ? value == null : value != null && value.BaseBuyValue == 0
                    && value.BaseSellValue == 0 && value.OverrideEvent == EventTimingType.SPECIAL_EVENT_NEVER),
                "grant-only native values absent: " + id + "/" + premium);
            Assert(!Permission(id, premium, ref allowed) && !allowed
                && !CraftingEvent(id, premium, ref future, ref eventActive) && !future && !eventActive
                && !CanCraft(new EntityDef { Id = id }, premium, ref craft) && craft == CraftingManager.CanCraftCardResult.NotCraftable,
                "grant-only native crafting buttons disabled: " + id + "/" + premium);
            Assert(!HasMassExtras(), "grant-only extras never enable native bulk sale");
        }
        foreach (string id in new[] { "CATA_EVENT_000", "BE_036" }) {
            var gifts = ResetHooks();
            gifts.Counts[id] = 2;
            CraftingManager.Get().Shown = new EntityDef { Id = id };
            Assert(!BeforeCreate(true) && !BeforeUpgradeEvent("CODE_UPGRADE") && gifts.Commits == 0
                && gifts.Counts[id] == 2 && NativeUIProbe.RewardProfile.OwnedCount(id, 1) == 0,
                "grant-only native upgrade refused before mutation: " + id);
        }
        RunUpgradeHooks();
        RunTokenHooks();
        var bulkLedger = ResetHooks();
        bulkLedger.Counts["TEST_CARD"] = 5;
        Assert(HasMassExtras(), "mass extras use authoritative ownership");
        Assert(bulkLedger.SnapshotReads == 1 && bulkLedger.SingleReads == 0,
            "mass extras must read exactly one ownership snapshot, no per-card profile reads");
        bulkLedger = ResetHooks();
        var changedIds = Enumerable.Range(0, 50).Select(i => "CARD_" + i).ToArray();
        foreach (string id in changedIds) bulkLedger.Counts[id] = 3;
        Synchronize(bulkLedger, changedIds.Concat(new[] { changedIds[0], null, "" }));
        Assert(bulkLedger.SnapshotReads == 1 && bulkLedger.SingleReads == 0
            && changedIds.All(id => CollectionManager.Get().Counts[id] == 3),
            "synchronize fifty cards from one snapshot and preserve deduplication");

        var settledLedger = ResetHooks();
        var settledProfile = new LocalProfile();
        NativeUIProbe.RewardProfile = settledProfile;
        settledLedger.DustBalance = 1040;
        SettlementDust(new LocalProfile());
        Assert(PublishedDust == -1, "another profile settlement must not publish selected balance");
        NativeUIProbe.Active = false;
        SettlementDust(settledProfile);
        Assert(PublishedDust == -1, "inactive native UI must not publish settlement");
        NativeUIProbe.Active = true;
        SettlementDust(settledProfile);
        Assert(PublishedDust == 1040 && Attempts.GetValueOrDefault("display") == 2,
            "settlement republishes full committed dust and refreshes displays");
        ResetHooks();
        foreach (var disabled in new[] { (false, (ICraftingLedger)null), (true, (ICraftingLedger)null), (false, new FakeCraftingLedger()) })
        {
            NativeUIProbe.Active = disabled.Item1;
            CraftingLedgerRegistry.Current = disabled.Item2;
            var result = CraftingManager.CanCraftCardResult.TooManyCopies;
            Assert(CanCraft(new EntityDef(), TAG_PREMIUM.NORMAL, ref result)
                && result == CraftingManager.CanCraftCardResult.TooManyCopies,
                "disabled eligibility preserves native result, active=" + disabled.Item1);
            var record = new CardValueDbfRecord();
            var price = new NetCache.CardValue();
            var originalRecord = record;
            var originalPrice = price;
            bool future = true, allowed = true;
            Assert(Price("TEST_CARD", TAG_PREMIUM.NORMAL, ref record, ref price)
                && ReferenceEquals(record, originalRecord) && ReferenceEquals(price, originalPrice),
                "disabled price hook preserves arguments");
            Assert(CraftingEvent("TEST_CARD", TAG_PREMIUM.NORMAL, ref future, ref allowed) && future && allowed,
                "disabled event hook preserves arguments");
            Assert(Permission("TEST_CARD", TAG_PREMIUM.NORMAL, ref allowed)
                && allowed, "disabled permission hooks preserve result");
            Assert(BeforeCreate(true) && BeforeDisenchant() && BeforeUpgradeEvent("CODE_UPGRADE"), "disabled action guards preserve native flow");
            int originals = 0;
            if (CommitBatch(null, 0, 0, 0, 0)) originals++;
            if (CommitMass()) originals++;
            Assert(originals == (disabled.Item1 ? 0 : 2),
                "transports never reach originals while offline probe active, ledger=" + (disabled.Item2 != null));
            var original = new Exception("native failure");
            Assert(InvokeCloseFailed(original) == original, "disabled finalizer preserves native exception");
        }

        var ledger = ResetHooks();
        var manager = CraftingManager.Get();
        var untouched = new Exception("unhandled native failure");
        Assert(InvokeCloseFailed(untouched) == untouched, "enabled finalizer preserves exception when hook did nothing");
        BeforeClose();
        var pending = new CraftingPendingTransaction { NormalCreateCount = 2 };
        Assert(!CommitBatch(pending, 0, 0, 0, 0), "offline batch intercepts transport");
        CommitBatch(pending, 0, 0, 0, 0);
        Assert(ledger.Commits == 1 && ledger.OwnedCount("TEST_CARD") == 2 && ledger.DustBalance == 920,
            "one durable commit per card-view close");
        Assert(Popups.Count == 1, "duplicate transport refused with one popup");
        Popups.Clear();
        AfterClose(manager);
        Assert(!closing && batch == null && manager.Clean && !manager.IsInCraftingMode
            && manager.Notifications == 2 && CollectionManager.Get().Counts["TEST_CARD"] == 2
            && PublishedDust == 920 && Popups.Count == 0 && Attempts.GetValueOrDefault("decks") == 1,
            "committed close clears overlays, notifies exact count and reprojects decks");

        ledger = ResetHooks();
        BeforeClose();
        CommitBatch(new CraftingPendingTransaction { NormalCreateCount = 1, Premium = TAG_PREMIUM.GOLDEN }, 0, 0, 0, 0);
        AfterClose(CraftingManager.Get());
        Assert(ledger.Commits == 1 && ledger.OwnedCount("TEST_CARD") == 1 && Popups.Count == 0,
            "Normal craft commits after switching to golden view");

        ledger = ResetHooks();
        ledger.Counts["TEST_CARD"] = 3;
        ledger.Counts["PROFILE_ONLY"] = int.MaxValue;
        CommitMass();
        Assert(ledger.Commits == 0 && ledger.OwnedCount("TEST_CARD") == 3
            && ledger.OwnedCount("PROFILE_ONLY") == int.MaxValue && ledger.DustBalance == 1000,
            "mass overflow in profile-only card refused before persistence");

        // Premium sale end to end. Two golden copies are progression rewards, a third came from elsewhere: only it sells.
        foreach (var view in new[] { TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.DIAMOND })
        {
            ledger = ResetHooks(); manager = CraftingManager.Get();
            NativeUIProbe.RewardProfile.Premium[("CORE_ONLY", 1)] = 3;
            NativeUIProbe.RewardProfile.Progression[("CORE_ONLY", 1)] = 2;
            CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] = 3;
            var sale = new CraftingPendingTransaction { CardID = "CORE_ONLY", GoldenDisenchantCount = 1, Premium = view };
            manager.m_pendingClientTransaction = sale;
            var shown = new CardValueDbfRecord(); NetCache.CardValue visible = null;
            Price("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref shown, ref visible);
            bool allowedNow = false, futureNow = true, eventNow = false;
            Permission("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref allowedNow);
            CraftingEvent("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref futureNow, ref eventNow);
            Assert(visible != null && visible.BaseSellValue == 200 && allowedNow && eventNow,
                "golden value and permission shown while one copy is disenchantable, view=" + view);
            BeforeClose();
            Assert(sale.Premium == TAG_PREMIUM.GOLDEN, "close prices the golden sale as golden, view=" + view);
            var record = new CardValueDbfRecord(); NetCache.CardValue value = null;
            Price("CORE_ONLY", sale.Premium, ref record, ref value);
            Assert(value != null && value.BaseSellValue == 200, "native price lookup succeeds for the golden sale, view=" + view);
            CommitBatch(sale, 0, 3, 0, 0);
            AfterClose(manager);
            Assert(NativeUIProbe.RewardProfile.OwnedCount("CORE_ONLY", 1) == 2 && CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] == 2
                && ledger.DustBalance == 1200 && PublishedDust == 1200 && Popups.Count == 0 && log.Errors.Count == 0
                && manager.Notifications == 1 && manager.LastSalePremium == TAG_PREMIUM.GOLDEN && manager.Clean && !closing,
                "extra golden copy sells at the table value, synchronizes the golden stack and notifies, view=" + view);
        }
        // Plain Normal crafting works with premium price objects present for every quality.
        ledger = ResetHooks();
        foreach (var quality in new[] { TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE })
        {
            var qualityRecord = new CardValueDbfRecord(); NetCache.CardValue qualityPrice = null;
            Price("TEST_CARD", quality, ref qualityRecord, ref qualityPrice);
            Assert(qualityPrice != null && qualityPrice.BaseSellValue == (quality == TAG_PREMIUM.NORMAL ? 20 : 0), "price object exists for every quality: " + quality);
        }
        var noNormalPrice = new CardValueDbfRecord(); NetCache.CardValue nullPrice = new();
        Price("CORE_ONLY", TAG_PREMIUM.NORMAL, ref noNormalPrice, ref nullPrice);
        Assert(nullPrice == null, "Core card has no Normal price, which is why close must switch Premium");

        // Only progression copies: no value is shown, a queued sale is refused with a message and nothing changes.
        foreach (var view in new[] { TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN })
        {
            ledger = ResetHooks(); manager = CraftingManager.Get();
            NativeUIProbe.RewardProfile.Premium[("CORE_ONLY", 1)] = 2;
            NativeUIProbe.RewardProfile.Progression[("CORE_ONLY", 1)] = 2;
            CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] = 2;
            var shown = new CardValueDbfRecord(); NetCache.CardValue visible = new();
            Price("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref shown, ref visible);
            bool allowedNow = true, futureNow = true;
            Permission("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref allowedNow);
            bool eventNow = true; CraftingEvent("CORE_ONLY", TAG_PREMIUM.GOLDEN, ref futureNow, ref eventNow);
            Assert(visible != null && visible.BaseSellValue == 0 && !allowedNow && !eventNow,
                "earned golden copies keep a zero-value price object and are not disenchantable");
            var sale = new CraftingPendingTransaction { CardID = "CORE_ONLY", GoldenDisenchantCount = 1, Premium = view };
            manager.m_pendingClientTransaction = sale;
            BeforeClose();
            var record = new CardValueDbfRecord(); NetCache.CardValue value = null;
            Price("CORE_ONLY", sale.Premium, ref record, ref value);
            Assert(value != null, "a queued premium sale is never dropped silently by native, view=" + view);
            CommitBatch(sale, 0, 2, 0, 0);
            AfterClose(manager);
            Assert(NativeUIProbe.RewardProfile.OwnedCount("CORE_ONLY", 1) == 2 && CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] == 2
                && ledger.DustBalance == 1000 && ledger.Commits == 0 && Popups.Count == 1 && manager.Notifications == 0 && manager.Clean,
                "progression golden disenchant is refused with a message and leaves ownership unchanged, view=" + view);
        }

        // Live fault: an earned Golden First Flame opened in crafting mode threw NullReferenceException. Offline its set is
        // INVALID (timing-only Core reprint), GameDbf has no CardSet record, and the native soulbound text read
        // record.IsFeaturedCardSet for every owned copy before drawing any button. Model that branch with the native getters,
        // then with the transpiled helpers, for opening the card, hovering (redraw), the Normal-view switch and close.
        Assert(Throws<NullReferenceException>(() => NativeSoulbound(null, record => record.IsFeaturedCardSet, record => record.IsCoreCardSet)),
            "model reproduces the native null card-set dereference");
        foreach (var view in new[] { TAG_PREMIUM.GOLDEN, TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN })
        {
            ledger = ResetHooks(); manager = CraftingManager.Get();
            NativeUIProbe.RewardProfile.Premium[("CORE_SW_108", 1)] = 2;
            NativeUIProbe.RewardProfile.Progression[("CORE_SW_108", 1)] = 2;
            manager.Shown = new EntityDef { Id = "CORE_SW_108" }; manager.ShownPremium = view;
            string desc = NativeSoulbound(null, FeaturedCardSet, CoreCardSet);
            var shown = new CardValueDbfRecord(); NetCache.CardValue visible = null;
            Price("CORE_SW_108", view, ref shown, ref visible);
            bool allowedNow = true, futureNow = true, eventNow = true;
            Permission("CORE_SW_108", view, ref allowedNow);
            CraftingEvent("CORE_SW_108", view, ref futureNow, ref eventNow);
            var craftable = CraftingManager.CanCraftCardResult.TooManyCopies;
            CanCraft(manager.Shown, view, ref craftable);
            Assert(desc == "GLUE_CRAFTING_SOULBOUND_CORE_DESC" && shown == null && !allowedNow && !eventNow
                && craftable == CraftingManager.CanCraftCardResult.NotCraftable,
                "earned Core golden shows the Core soulbound text with Create/Disenchant disabled, view=" + view);
            BeforeClose();
            AfterClose(manager);
            Assert(manager.Clean && !closing && Popups.Count == 0 && log.Errors.Count == 0 && ledger.Commits == 0,
                "closing the earned golden view commits nothing and leaves native clean, view=" + view);
        }
        manager.Shown = new EntityDef { Id = "TEST_CARD" };
        Assert(NativeSoulbound(null, FeaturedCardSet, CoreCardSet) == "GLUE_CRAFTING_SOULBOUND_DESC",
            "a missing set record on a non-Core card falls back to the generic soulbound text");
        Assert(NativeSoulbound(new CardSetDbfRecord { IsFeaturedCardSet = true }, FeaturedCardSet, CoreCardSet) == "GLUE_CRAFTING_SOULBOUND_FEATURED_DESC"
            && NativeSoulbound(new CardSetDbfRecord { IsCoreCardSet = true }, FeaturedCardSet, CoreCardSet) == "GLUE_CRAFTING_SOULBOUND_CORE_DESC"
            && NativeSoulbound(new CardSetDbfRecord(), FeaturedCardSet, CoreCardSet) == "GLUE_CRAFTING_SOULBOUND_DESC",
            "a present set record keeps the native soulbound choice");
        CraftingManager.Instance = null;
        Assert(NativeSoulbound(null, FeaturedCardSet, CoreCardSet) == "GLUE_CRAFTING_SOULBOUND_DESC", "no crafting manager is not Core");

        ledger = ResetHooks(); manager = CraftingManager.Get();
        NativeUIProbe.RewardProfile.Premium[("CORE_ONLY", 1)] = 3;
        CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] = 3;
        BeforeClose();
        CommitBatch(new CraftingPendingTransaction { CardID = "CORE_ONLY", GoldenDisenchantCount = 1 }, 0, 1, 0, 0);
        AfterClose(manager);
        Assert(NativeUIProbe.RewardProfile.OwnedCount("CORE_ONLY", 1) == 3 && CollectionManager.Get().PremiumCounts[("CORE_ONLY", TAG_PREMIUM.GOLDEN)] == 3
            && ledger.DustBalance == 1000 && Popups.Count == 1, "stale golden count is refused and native keeps every copy");

        foreach (string failure in new[] { "dust", "bank", "display", "page", "locks", "sync", "decks", "callback", "all", "close", "refusal" })
        {
            ledger = ResetHooks(); manager = CraftingManager.Get();
            ledger.Refuse = failure == "refusal";
            BeforeClose(); CommitBatch(pending, 0, 0, 0, 0);
            Failures.Add(failure == "refusal" ? "dust" : failure);
            if (failure == "all") Failures.UnionWith(new[] { "dust", "bank", "display", "page", "locks", "sync", "callback" });
            if (failure == "close") Assert(InvokeCloseFailed(new Exception("native close failure")) == null,
                "handled offline close exception suppressed");
            else AfterClose(manager);
            Assert(!closing && batch == null && manager.Clean && !manager.IsInCraftingMode,
                failure + " cannot strand pending state or crafting mode");
            Assert(Attempts.GetValueOrDefault("dust") == 1 && Attempts.GetValueOrDefault("page") == 1 && Attempts.GetValueOrDefault("sync") == 1,
                failure + " refresh steps each attempted once");
            if (failure != "refusal")
            {
                Assert(Popups.Count == 1 && Popups[0].Contains("saved", StringComparison.OrdinalIgnoreCase)
                    && Popups[0].Contains("refresh", StringComparison.OrdinalIgnoreCase),
                    failure + " reports one saved-progress refresh warning");
                Assert(ledger.Commits == 1 && ledger.OwnedCount("TEST_CARD") == 2 && ledger.DustBalance == 920,
                    failure + " leaves committed ledger authoritative");
                foreach (string step in Failures.Where(step => step != "all" && step != "close"))
                    Assert(log.Errors.Count(error => error.Contains(step + " failure")) == Attempts.GetValueOrDefault(step),
                        "every " + step + " failure logged");
                Assert(Attempts.GetValueOrDefault("bank") == 1 && Attempts.GetValueOrDefault("display") == 2
                    && Attempts.GetValueOrDefault("locks") == 1 && Attempts.GetValueOrDefault("callback") == 2,
                    failure + " continues nested refresh steps and callbacks");
            }
            if (failure == "refusal") Assert(Popups.Count > 0 && ledger.Commits == 0
                && ledger.DustBalance == 1000 && ledger.OwnedCount("TEST_CARD") == 0,
                "refusal shows error and leaves ledger unchanged despite refresh failure");
        }

        foreach (bool refused in new[] { false, true })
        {
            ledger = ResetHooks(); ledger.Counts["TEST_CARD"] = 5; CollectionManager.Get().Counts["TEST_CARD"] = 5; ledger.Refuse = refused;
            Failures.Add("dust");
            int delivered = 0;
            CollectionManager.Get().m_massDisenchantListeners.Add(_ => throw new Exception("listener failure"));
            CollectionManager.Get().m_massDisenchantListeners.Add(amount => delivered = amount);
            CommitMass();
            Assert(ledger.Commits == (refused ? 0 : 1) && ledger.OwnedCount("TEST_CARD") == (refused ? 5 : 2)
                && ledger.DustBalance == (refused ? 1000 : 1060), "mass is atomic, refused=" + refused);
            Assert(!MassDisenchant.Get().Blocked && MassDisenchant.Get().m_disenchantButton.Enabled == refused
                && massResponse == null && Attempts.GetValueOrDefault("dust") == 1 && Attempts.GetValueOrDefault("page") == 1,
                "mass restores input and enables retry only when extras remain, refused=" + refused);
            Assert(refused ? Popups.Count > 0 : delivered == 60, "mass refusal popup or full listener fan-out");
        }

        foreach (string failure in new[] { "sync", "dust", "bank", "display", "page", "locks", "listener", "all" })
        {
            ledger = ResetHooks(); ledger.Counts["TEST_CARD"] = 5; CollectionManager.Get().Counts["TEST_CARD"] = 5;
            Failures.Add(failure);
            if (failure == "all") Failures.UnionWith(new[] { "sync", "dust", "bank", "display", "page", "locks", "listener" });
            int delivered = 0;
            CollectionManager.Get().m_massDisenchantListeners.Add(_ => Boundary("listener"));
            CollectionManager.Get().m_massDisenchantListeners.Add(amount => delivered = amount);
            CommitMass();
            Assert(ledger.Commits == 1 && ledger.OwnedCount("TEST_CARD") == 2 && ledger.DustBalance == 1060
                && delivered == 60, failure + " mass commits once and continues listener fan-out");
            Assert(Popups.Count == 1 && Popups[0].Contains("saved", StringComparison.OrdinalIgnoreCase)
                && Popups[0].Contains("refresh", StringComparison.OrdinalIgnoreCase) && !MassDisenchant.Get().Blocked,
                failure + " nested mass failure triggers saved-progress recovery warning");
            foreach (string step in Failures.Where(step => step != "all"))
                Assert(log.Errors.Count(error => error.Contains(step + " failure")) == Attempts.GetValueOrDefault(step),
                    "every mass " + step + " failure logged");
            MassUnblocked(false);
            Assert(Popups.Count == 1, "mass completion does not repeat recovery warning");
        }

        ledger = ResetHooks(); ledger.Counts["TEST_CARD"] = 5; CollectionManager.Get().Counts["TEST_CARD"] = 5;
        int received = 0;
        CollectionManager.Get().m_massDisenchantListeners.Add(amount => received = amount);
        CommitMass();
        Assert(ledger.Commits == 1 && received == 60 && massResponse == null && PublishedDust == 1060
            && CollectionManager.Get().Counts["TEST_CARD"] == 2 && Popups.Count == 0,
            "normal mass completes native dispatch once");
        Assert(Attempts.GetValueOrDefault("decks") == 1, "mass reprojects affected decks");
        Failures.Add("dust");
        MassUnblocked(false);
        Assert(Attempts.GetValueOrDefault("page") == 2 && !MassDisenchant.Get().m_disenchantButton.Enabled
            && Popups.Count == 1 && Popups[0].Contains("saved", StringComparison.OrdinalIgnoreCase),
            "animation unblock keeps empty button disabled and warns on final dust failure");
        MassUnblocked(false);
        Assert(Popups.Count == 1, "one refresh warning across mass recovery and animation completion");

        foreach (var sample in new[] {
            (0, 1000L, false, false), (2, 1000L, false, false), (3, 1000L, false, true),
            (int.MaxValue, 0L, false, false), (3, (long)int.MaxValue - 10, false, false),
            (3, (long)int.MaxValue - 20, false, true), (3, 1000L, true, false) })
        {
            ledger = ResetHooks(); ledger.Counts["TEST_CARD"] = sample.Item1;
            ledger.DustBalance = sample.Item2; ledger.PackRevealPending = sample.Item3;
            // Native counts can be stale after a failed synchronization; the ledger decides eligibility.
            CollectionManager.Get().Counts["TEST_CARD"] = 5;
            MassDisenchant.Get().m_disenchantButton.SetEnabled(true);
            MassUnblocked(false);
            Assert(MassDisenchant.Get().m_disenchantButton.Enabled == sample.Item4,
                "animation button requires valid ledger extras: " + sample);
        }

        // Reflection keeps this regression executable against the pre-fix source too.
        var totals = typeof(NativeCrafting).GetMethod("TryMassTotals", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(totals != null, "mass totals guard exists before native preview math");
        foreach (var sample in new[] {
            (new[] { new KeyValuePair<int, int>(3, 20), new KeyValuePair<int, int>(2, 800) }, 17L, true, 1660L),
            (new[] { new KeyValuePair<int, int>(int.MaxValue, 20) }, 0L, false, 0L),
            (new[] { new KeyValuePair<int, int>(1, 20) }, (long)int.MaxValue - 10, false, 0L),
            (new[] { new KeyValuePair<int, int>(1, 20) }, long.MaxValue, false, 0L),
            (new[] { new KeyValuePair<int, int>(-1, 20) }, 0L, false, 0L),
            (new[] { new KeyValuePair<int, int>(int.MaxValue, 0), new KeyValuePair<int, int>(1, 0) }, 0L, false, 0L),
            (Enumerable.Repeat(new KeyValuePair<int, int>(int.MaxValue, int.MaxValue), 3).ToArray(), 0L, false, 0L),
            (new[] { new KeyValuePair<int, int>(1, 20) }, (long)int.MaxValue - 20, true, 20L) })
        {
            object[] values = { sample.Item1, sample.Item2, 0L };
            bool ok = (bool)totals.Invoke(null, values);
            Assert(ok == sample.Item3 && (!ok || (long)values[2] == sample.Item4), "checked mass total boundary");
        }
        var preview = typeof(NativeCrafting).GetMethod("PreviewMass", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(preview != null, "preview validation hook exists");
        ledger = ResetHooks();
        // Native counts every version (OwnedCount here holds 100 premium-inflated copies); the preview lists ledger extras only.
        var cards = new List<CollectibleCard> { new() { CardId = "TEST_CARD", PremiumType = TAG_PREMIUM.NORMAL, OwnedCount = 100 } };
        object[] previewArgs = { MassDisenchant.Get(), cards };
        preview.Invoke(null, previewArgs);
        Assert(((List<CollectibleCard>)previewArgs[1]).Count == 0 && cards.Count == 1 && cards[0].OwnedCount == 100
            && !MassDisenchant.Get().m_disenchantButton.Enabled,
            "premium-inflated native count with no Normal extras previews nothing to sell");
        ledger.Counts["TEST_CARD"] = 5; cards[0].OwnedCount = 2; previewArgs[1] = cards;
        MassDisenchant.Get().m_disenchantButton.SetEnabled(true);
        preview.Invoke(null, previewArgs);
        var shownCards = (List<CollectibleCard>)previewArgs[1];
        Assert(shownCards.Count == 1 && shownCards[0].IsCraftableDisenchantCount == 3
            && shownCards.Sum(card => card.IsCraftableDisenchantCount * 20) == 60 && cards[0].OwnedCount == 2
            && MassDisenchant.Get().m_disenchantButton.Enabled,
            "preview lists the ledger's three Normal extras (60 dust) on a copy, even when native reports none");
        ledger.Counts["TEST_CARD"] = int.MaxValue; cards[0].OwnedCount = 0; previewArgs[1] = cards;
        preview.Invoke(null, previewArgs);
        Assert(((List<CollectibleCard>)previewArgs[1]).Count == 0 && cards.Count == 1
            && !MassDisenchant.Get().m_disenchantButton.Enabled,
            "overflow preview clears only native input before unchecked math");
        CommitMass();
        Assert(ledger.Commits == 0 && Popups.Count > 0 && ledger.OwnedCount("TEST_CARD") == int.MaxValue,
            "overflow refused before durable mass commit");
        CraftingLedgerRegistry.Current = null;
        previewArgs[1] = cards;
        preview.Invoke(null, previewArgs);
        Assert(ReferenceEquals(previewArgs[1], cards), "disabled preview preserves exact input");
    }

    private static void RunUpgradeHooks()
    {
        var ledger = ResetHooks(); var manager = CraftingManager.Get();
        manager.Shown = new EntityDef();
        var record = new CardValueDbfRecord(); NetCache.CardValue value = null;
        Price("TEST_CARD", TAG_PREMIUM.NORMAL, ref record, ref value);
        Assert(value.BaseUpgradeValue == 360, "Normal price exposes retail upgrade tariff");
        bool allowed = false, future = true;
        Permission("TEST_CARD", TAG_PREMIUM.GOLDEN, ref allowed);
        CraftingEvent("TEST_CARD", TAG_PREMIUM.GOLDEN, ref future, ref allowed);
        Assert(allowed && !future, "Golden presentation permits upgrades before any Golden is owned");
        var result = CraftingManager.CanCraftCardResult.TooManyCopies;
        Assert(!CanCraft(manager.Shown, TAG_PREMIUM.GOLDEN, ref result) && result == CraftingManager.CanCraftCardResult.NotCraftable,
            "Golden crafting from scratch stays refused");
        foreach (string id in new[] { "CORE_ONLY", "UNKNOWN" }) {
            Price(id, TAG_PREMIUM.GOLDEN, ref record, ref value);
            Assert(value.BaseUpgradeValue == 0, "premium has no upgrade tariff: " + id);
        }

        foreach (var pending in new[] {
            new CraftingPendingTransaction { NormalCreateCount = 1 },
            new CraftingPendingTransaction { NormalDisenchantCount = 1 },
            new CraftingPendingTransaction { GoldenDisenchantCount = 1 },
            new CraftingPendingTransaction { GoldenUpgradeFromNormalCount = 1 } }) {
            ledger = ResetHooks(); manager = CraftingManager.Get(); manager.Shown = new EntityDef();
            ledger.Counts["TEST_CARD"] = 2;
            manager.m_pendingClientTransaction = pending;
            bool upgrade = pending.GoldenUpgradeFromNormalCount > 0;
            Assert(BeforeUpgradeEvent("CODE_UPGRADE") == upgrade, "popup refuses upgrade after another kind, allows repeated upgrade");
            Assert(BeforeUpgradeEvent("CODE_CREATE") == (pending.NormalCreateCount > 0), "popup refuses create after another kind");
            if (pending.GetLastTransactionWasCrafting())
                Assert(BeforeDisenchant(), "Disenchant Undo passes after craft or upgrade");
            else Assert(BeforeCreate(false) && BeforeCreate(true), "Create Undo passes after any sale");
            Assert(manager.DustAdjustment == -80 && ledger.Commits == 0, "action guards do not change dust or durable counts");
        }
        ledger = ResetHooks(); manager = CraftingManager.Get(); manager.Shown = new EntityDef();
        manager.m_pendingClientTransaction.NormalCreateCount = 1;
        Assert(!BeforeCreate(true) && BeforeCreate(false), "DoCreate refuses upgrade after Normal create, allows same create");
        Assert(Popups.Single() == "Close the card to save this change, then reopen it for a different crafting action.", "mixed action asks to save and reopen");
        manager.m_pendingClientTransaction = new CraftingPendingTransaction { GoldenUpgradeFromNormalCount = 1 };
        ledger.Counts["TEST_CARD"] = 2;
        manager.ShownPremium = TAG_PREMIUM.GOLDEN;
        Assert(BeforeCreate(true) && BeforeUpgradeEvent("CODE_UPGRADE") && !BeforeCreate(false), "Golden view repeats upgrades and refuses GoldenCreate");
        manager.m_pendingClientTransaction = new CraftingPendingTransaction { NormalDisenchantCount = 1 };
        Assert(!BeforeDisenchant(), "premium sale cannot mix with Normal sale after switching view");

        foreach (bool popup in new[] { false, true })
        foreach (var scenario in new[] { "cap", "dust", "pending-dust", "normal", "pack", "repeat", "infinite" })
        {
            ledger = ResetHooks(); manager = CraftingManager.Get(); manager.Shown = new EntityDef();
            ledger.Counts["TEST_CARD"] = scenario == "normal" ? 1 : 2;
            NativeUIProbe.RewardProfile.Premium[("TEST_CARD", 1)] = scenario == "cap" ? 2 : 0;
            ledger.DustBalance = scenario == "dust" || scenario == "infinite" ? 359 : scenario == "pending-dust" ? 719 : 1000;
            NativeUIProbe.RewardProfile.InfiniteDust = scenario == "infinite";
            ledger.PackRevealPending = scenario == "pack";
            if (scenario == "repeat" || scenario == "pending-dust" || scenario == "normal")
                manager.m_pendingClientTransaction.GoldenUpgradeFromNormalCount = 1;
            bool pass = scenario == "repeat" || scenario == "infinite";
            string expectedError = scenario == "cap" ? "Golden upgrade limit reached."
                : scenario == "normal" ? "Not enough Normal copies to upgrade."
                : scenario == "pack" ? "Finish the pending pack reveal first." : "Not enough dust.";
            Assert((popup ? BeforeUpgradeEvent("CODE_UPGRADE") : BeforeCreate(true)) == pass,
                "upgrade pre-check covers full pending batch: " + (popup, scenario));
            Assert(manager.DustAdjustment == -80 && ledger.Commits == 0 && ledger.Counts["TEST_CARD"] == (scenario == "normal" ? 1 : 2)
                && (pass ? Popups.Count == 0 : Popups.Single() == expectedError), "upgrade pre-check refuses before native mutation and shows planner error");
        }

        // A craftable earned-only Golden still needs upgrade presentation, but no sale or sale animation.
        foreach (TAG_PREMIUM premium in new[] { TAG_PREMIUM.GOLDEN, TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE }) {
            ledger = ResetHooks(); manager = CraftingManager.Get(); manager.Shown = new EntityDef(); manager.ShownPremium = premium;
            NativeUIProbe.RewardProfile.Premium[("TEST_CARD", (int)premium)] = 2;
            NativeUIProbe.RewardProfile.Progression[("TEST_CARD", (int)premium)] = 2;
            var ui = new CraftingUI(); ui.m_buttonDisenchant.SetEnabled(true);
            ProtectPremiumButton(ui);
            Assert(!ui.m_buttonDisenchant.Enabled && !ui.DisenchantCostBar && !BeforeDisenchant()
                && manager.DustAdjustment == -80 && ledger.Commits == 0, "granted-only premium disables sale and cost bar before mutation: " + premium);
            if (premium == TAG_PREMIUM.GOLDEN) {
                Permission("TEST_CARD", premium, ref allowed);
                Assert(allowed, "earned-only craftable Golden still exposes upgrade eligibility");
                manager.m_pendingClientTransaction.GoldenUpgradeFromNormalCount = 1;
                ui.m_buttonDisenchant.SetEnabled(true); ui.DisenchantCostBar = true;
                ProtectPremiumButton(ui);
                Assert(ui.m_buttonDisenchant.Enabled && ui.DisenchantCostBar && BeforeDisenchant(), "upgrade Undo survives earned-only Golden protection");
            }
            manager.m_pendingClientTransaction = new CraftingPendingTransaction();
            NativeUIProbe.RewardProfile.Progression[("TEST_CARD", (int)premium)] = 1;
            ui.m_buttonDisenchant.SetEnabled(true); ui.DisenchantCostBar = true;
            ProtectPremiumButton(ui);
            Assert(ui.m_buttonDisenchant.Enabled && ui.DisenchantCostBar && BeforeDisenchant(), "one premium extra can be sold: " + premium);
            if (premium == TAG_PREMIUM.GOLDEN) manager.m_pendingClientTransaction.GoldenDisenchantCount = 1;
            else if (premium == TAG_PREMIUM.DIAMOND) manager.m_pendingClientTransaction.DiamondDisenchantCount = 1;
            else manager.m_pendingClientTransaction.SignatureDisenchantCount = 1;
            ProtectPremiumButton(ui);
            Assert(!ui.m_buttonDisenchant.Enabled && !ui.DisenchantCostBar && !BeforeDisenchant(), "pending sales exhaust extras, preserve grant: " + premium);
        }

        foreach (bool refuse in new[] { false, true }) foreach (int count in new[] { 1, 2 }) {
            ledger = ResetHooks(); manager = CraftingManager.Get(); ledger.Counts["TEST_CARD"] = 2;
            ledger.Refuse = refuse;
            CollectionManager.Get().Counts["TEST_CARD"] = refuse ? 0 : 2;
            CollectionManager.Get().PremiumCounts[("TEST_CARD", TAG_PREMIUM.GOLDEN)] = refuse ? 2 : 0;
            var pending = new CraftingPendingTransaction { GoldenUpgradeFromNormalCount = count };
            manager.m_pendingClientTransaction = pending;
            BeforeClose();
            Assert(pending.Premium == TAG_PREMIUM.GOLDEN, "close uses Golden card definition for upgrade");
            CommitBatch(pending, 2, 0, 123, 456);
            AfterClose(manager);
            Assert(ledger.Commits == (refuse ? 0 : 1) && ledger.Counts["TEST_CARD"] == (refuse ? 2 : 2 - count)
                && NativeUIProbe.RewardProfile.OwnedCount("TEST_CARD", 1) == (refuse ? 0 : count)
                && PublishedDust == (refuse ? 1000 : 1000 - 360 * count), "upgrade routes expected Normal/Golden counts and dust to ledger");
            var changes = CollectionManager.Get().LastChanges;
            Assert(Attempts.GetValueOrDefault("modify") == 1 && changes.Count == 2
                && changes.Any(c => c.premium == TAG_PREMIUM.NORMAL) && changes.Any(c => c.premium == TAG_PREMIUM.GOLDEN)
                && CollectionManager.Get().Counts["TEST_CARD"] == ledger.Counts["TEST_CARD"]
                && CollectionManager.Get().PremiumCounts[("TEST_CARD", TAG_PREMIUM.GOLDEN)] == (refuse ? 0 : count),
                "upgrade reconciles both stacks in one native change call, refused=" + refuse);
            Assert(manager.Notifications == (refuse ? 0 : count) && manager.Clean && !closing
                && (refuse ? Popups.Count == 1 : manager.LastSalePremium == TAG_PREMIUM.GOLDEN
                    && manager.LastSaleAction == Network.CardSaleResult.SaleResult.CARD_WAS_UPGRADED && Attempts.GetValueOrDefault("cosmetics") == 1),
                "upgrade completion notifies each copy only on durable success, refused=" + refuse);
            Assert(Attempts.GetValueOrDefault("upgrade decks") == (refuse ? 0 : 1), "only committed upgrades refresh deck premiums");
        }
        ledger = ResetHooks(); manager = CraftingManager.Get(); ledger.Counts["TEST_CARD"] = 2;
        Failures.Add("upgrade decks");
        BeforeClose();
        CommitBatch(new CraftingPendingTransaction { GoldenUpgradeFromNormalCount = 1 }, 2, 0, 0, 0);
        AfterClose(manager);
        Assert(ledger.Commits == 1 && manager.Clean && manager.Notifications == 1 && Popups.Single().Contains("Your progress is saved"),
            "deck refresh failure preserves commit, clears pending and reports refresh error");
    }

    // #74: a held craft token makes one Normal copy for 0 dust; native shows it free, allows it without dust and never
    // offers a second token-priced copy in the same card view. Golden and other premium views keep their prices.
    private static void RunTokenHooks()
    {
        var ledger = ResetHooks(); var manager = CraftingManager.Get(); manager.Shown = new EntityDef();
        ledger.Tokens.Add("TEST_CARD"); ledger.DustBalance = 0;
        var record = new CardValueDbfRecord(); NetCache.CardValue value = null;
        Assert(!Price("TEST_CARD", TAG_PREMIUM.NORMAL, ref record, ref value) && value.BaseBuyValue == 0 && value.BaseSellValue == 20,
            "token-paid Normal craft is priced 0; its sell value stays ordinary");
        NativeUIProbe.RewardProfile.Premium[("TEST_CARD", 1)] = 1;
        Price("TEST_CARD", TAG_PREMIUM.GOLDEN, ref record, ref value);
        Assert(value.BaseSellValue == 200, "premium price untouched by a held token");
        NativeUIProbe.RewardProfile.Premium.Clear();
        Price("PROFILE_ONLY", TAG_PREMIUM.NORMAL, ref record, ref value);
        Assert(value.BaseBuyValue == 40, "a token for another card's rarity leaves its price");
        var craft = CraftingManager.CanCraftCardResult.NotEnoughDust;
        Assert(CanCraft(new EntityDef(), TAG_PREMIUM.NORMAL, ref craft), "token craft is handed to native's own craftability check");

        var ui = new CraftingUI(); ui.m_buttonCreate.SetEnabled(true); ui.m_craftValue.Text = "-0";
        ShowTokenCraft(ui);
        Assert(ui.m_craftValue.Text == "0" && ui.m_buttonCreate.Enabled && ui.CreateCostBar, "without the client's free word the label reads 0, unsigned");
        GameStrings.Translations["GLUE_SHOP_FREE"] = "Gratis";
        ui.m_craftValue.Text = "-0";
        ShowTokenCraft(ui);
        GameStrings.Translations.Remove("GLUE_SHOP_FREE");
        Assert(ui.m_craftValue.Text == "Gratis" && TokenCraftLabel() == "0", "token craft reads the client's localized free word"); 
        Assert(BeforeCreate(false) && BeforeUpgradeEvent("CODE_CREATE") && Popups.Count == 0, "first token copy may be created");
        manager.m_pendingClientTransaction.NormalCreateCount = 1;
        Assert(!BeforeCreate(false) && !BeforeUpgradeEvent("CODE_CREATE") && Popups.Count == 2 && Popups.All(text => text == TokenOneCopy)
            && manager.DustAdjustment == -80 && ledger.Commits == 0, "second token-priced copy refused before native mutation");
        ui.m_craftValue.Text = "-0";
        ShowTokenCraft(ui);
        Assert(!ui.m_buttonCreate.Enabled && !ui.CreateCostBar, "pending token copy disables Create; Undo stays on Disenchant");
        var broken = new CraftingUI(); broken.m_buttonCreate.SetEnabled(true); broken.m_buttonCreate.Throws = true;
        int errors = log.Errors.Count;
        ShowTokenCraft(broken);
        Assert(log.Errors.Count == errors + 1 && !BeforeCreate(false) && Price("TEST_CARD", TAG_PREMIUM.NORMAL, ref record, ref value) == false
            && value.BaseBuyValue == 0, "a missing client member logs; the 0 price and second-copy refusal still hold");
        Assert(BeforeDisenchant(), "Undo of the token copy passes");

        ui = new CraftingUI(); ui.m_buttonCreate.SetEnabled(true);
        manager.m_pendingClientTransaction = new CraftingPendingTransaction { NormalDisenchantCount = 1 };
        ui.m_craftValue.Text = "-20";
        ShowTokenCraft(ui);
        Assert(ui.m_craftValue.Text == "-20" && ui.m_buttonCreate.Enabled, "Create Undo of a sale keeps its native sell value");
        manager.m_pendingClientTransaction = new CraftingPendingTransaction();
        manager.ShownPremium = TAG_PREMIUM.GOLDEN; ui.m_craftValue.Text = "-0";
        ShowTokenCraft(ui);
        Assert(ui.m_craftValue.Text == "-0" && ui.m_buttonCreate.Enabled, "Golden view never reads free");
        manager.ShownPremium = TAG_PREMIUM.NORMAL; ledger.Tokens.Clear(); ui.m_craftValue.Text = "-40";
        ShowTokenCraft(ui);
        manager.m_pendingClientTransaction.NormalCreateCount = 1;
        Assert(ui.m_craftValue.Text == "-40" && BeforeCreate(false) && Popups.Count == 3, "no token: native price and repeated crafts unchanged");

        ledger = ResetHooks(); manager = CraftingManager.Get(); manager.Shown = new EntityDef();
        ledger.Tokens.Add("TEST_CARD"); ledger.DustBalance = 0;
        var pending = new CraftingPendingTransaction { NormalCreateCount = 1 };
        manager.m_pendingClientTransaction = pending;
        BeforeClose();
        CommitBatch(pending, 0, 0, 0, 0);
        AfterClose(manager);
        Assert(ledger.Commits == 1 && ledger.Counts["TEST_CARD"] == 1 && ledger.DustBalance == 0 && ledger.Tokens.Count == 0
            && PublishedDust == 0 && manager.Clean && manager.Notifications == 1 && Popups.Count == 0,
            "token craft commits on close with insufficient dust; displayed dust is the unchanged saved balance");

        NativeUIProbe.Active = false;
        ui = new CraftingUI(); ui.m_craftValue.Text = "-0";
        ShowTokenCraft(ui);
        Assert(ui.m_craftValue.Text == "-0", "disabled hook leaves native text");
    }

    private static Exception InvokeCloseFailed(Exception original)
    {
        var method = typeof(NativeCrafting).GetMethod("CloseFailed", BindingFlags.NonPublic | BindingFlags.Static);
        var result = method.Invoke(null, new object[] { CraftingManager.Get(), original });
        return method.ReturnType == typeof(void) ? original : result as Exception;
    }
}

internal sealed class FakeCraftingLedger : ICraftingLedger
{
    public long DustBalance { get; internal set; } = 1000;
    public bool PackRevealPending { get; set; }
    internal readonly Dictionary<string, int> Counts = new();
    internal int Commits;
    internal bool Refuse;
    internal int SnapshotReads, SingleReads;
    internal readonly HashSet<string> Tokens = new();
    public bool CraftsWithToken(string id) => Tokens.Contains(id);
    public IReadOnlyDictionary<string, int> OwnedCounts() { SnapshotReads++; return new Dictionary<string, int>(Counts); }
    public int OwnedCount(string id) { SingleReads++; return Counts.GetValueOrDefault(id); }
    public CraftPlan CommitBatch(string id, CraftDirection direction, int count, int expected)
    {
        if (Refuse || OwnedCount(id) != expected) return new() { Error = "refused" };
        int delta = direction == CraftDirection.Craft ? count : -count;
        bool token = direction == CraftDirection.Craft && Tokens.Contains(id);
        if (token && count != 1) return new() { Error = "refused" };
        long dust = token ? 0 : direction == CraftDirection.Craft ? -40L * count : 20L * count;
        if (DustBalance + dust < 0) return new() { Error = "refused" };
        if (token) Tokens.Remove(id);
        Counts[id] = OwnedCount(id) + delta; DustBalance += dust; Commits++;
        return new() { Ok = true, DustDelta = dust, Cards = new() { new() { CardId = id } } };
    }
    public CraftPlan PlanMassDisenchant()
    {
        var plan = new CraftPlan { Ok = !Refuse, Error = "refused" };
        foreach (var pair in Counts.Where(pair => pair.Value > 2))
        {
            int extras = pair.Value - 2;
            plan.Cards.Add(new() { CardId = pair.Key, CountDelta = -extras });
            plan.DustDelta += 20L * extras;
        }
        return plan;
    }
    public CraftPlan CommitMassDisenchant()
    {
        if (Refuse) return new() { Error = "refused" };
        var plan = PlanMassDisenchant();
        foreach (var card in plan.Cards) Counts[card.CardId] += card.CountDelta;
        DustBalance += plan.DustDelta;
        Commits++;
        return plan;
    }
}
internal sealed class LocalProfile
{
    internal bool InfiniteDust;
    internal IReadOnlyDictionary<string, int> Acquired => new Dictionary<string, int>();
    internal IEnumerable<LocalCardCount> OwnedCards => Premium.Select(row => new LocalCardCount { Id = row.Key.Item1, Premium = row.Key.Item2, Acquired = row.Value });
    internal readonly Dictionary<(string, int), int> Premium = new();
    internal readonly Dictionary<(string, int), int> Progression = new();
    internal int OwnedCount(string id, int premium) => Premium.GetValueOrDefault((id, premium));
    internal int DisenchantablePremiumCount(string id, int premium) => Math.Max(0, OwnedCount(id, premium) - Progression.GetValueOrDefault((id, premium)));
}
internal static class NativeUIProbe
{
    internal static bool Active;
    internal static LocalProfile RewardProfile;
    internal static void RefreshCosmetics() => NativeCrafting.Boundary("cosmetics");
    internal static void RefreshUpgradedDecks(string cardId)
    {
        if (CollectionManager.Get().PremiumCounts.GetValueOrDefault((cardId, TAG_PREMIUM.GOLDEN)) != RewardProfile.OwnedCount(cardId, 1))
            throw new Exception("deck refresh ran before ownership synchronization");
        NativeCrafting.Boundary("upgrade decks");
    }
    internal static void ApplyCraftingChanges(List<CollectionManager.CardModification> changes)
    {
        try { CollectionManager.Get().OnCardsModified(changes); }
        finally { NativeCrafting.Boundary("decks"); }
    }
}
internal sealed class LocalCardCount { internal string Id; internal int Premium, Acquired; }
internal static class ProfileCatalog { internal const long UnlimitedDust = 999999; }
internal static partial class LocalCrafting
{
    private static CraftPlan Refuse(string error) => new() { Error = error };
    private static int Owned(IReadOnlyDictionary<string, int> counts, string id) => counts.GetValueOrDefault(id);
    private static bool ValidView(CraftLedgerView view) => view != null && view.Dust >= 0 && view.Counts != null && view.Acquired != null;
    internal static bool TryGetUpgradePrice(string id, out int cost) { cost = id == "TEST_CARD" ? 360 : 0; return cost > 0; }
    internal static CraftPlan CommitUpgrade(LocalProfile profile, string id, int count, int expectedNormal, int expectedGolden)
    {
        var ledger = (FakeCraftingLedger)CraftingLedgerRegistry.Current;
        if (ledger.Refuse || ledger.OwnedCount(id) != expectedNormal || profile.OwnedCount(id, 1) != expectedGolden
            || count < 1 || count > expectedNormal || expectedGolden + count > 2 || ledger.DustBalance < count * 360)
            return new() { Error = "refused" };
        ledger.Counts[id] -= count; profile.Premium[(id, 1)] = expectedGolden + count;
        ledger.DustBalance -= 360L * count; ledger.Commits++;
        return new() { Ok = true, DustDelta = -360L * count, Cards = new() {
            new() { CardId = id, Premium = 0, CountDelta = -count }, new() { CardId = id, Premium = 1, CountDelta = count } } };
    }
    internal static bool TryGetPrice(string id, out int buy, out int sell) { bool ok = id == "TEST_CARD" || id == "PROFILE_ONLY"; buy = ok ? 40 : 0; sell = ok ? 20 : 0; return ok; }
    // CORE_ONLY has no Normal price (Core card) but golden and higher versions sell.
    internal static bool TryGetPrice(string id, int premium, out int buy, out int sell)
    { bool ok = TryGetPrice(id, out buy, out sell) || (id == "CORE_ONLY" && premium != 0); ok = ok && premium >= 0 && premium <= 3; if (premium != 0) { buy = 0; sell = ok ? 200 : 0; } return ok; }
    internal static CraftPlan CommitPremiumBatch(LocalProfile profile, string id, int premium, CraftDirection direction, int count, int expected)
    {
        if (profile.OwnedCount(id, premium) != expected || direction != CraftDirection.Disenchant || count < 1
            || count > profile.DisenchantablePremiumCount(id, premium))
            return new CraftPlan { Error = "refused" };
        profile.Premium[(id, premium)] = expected - count;
        var ledger = (FakeCraftingLedger)CraftingLedgerRegistry.Current;
        ledger.DustBalance += 200L * count; ledger.Commits++;
        return new CraftPlan { Ok = true, DustDelta = 200L * count, Cards = new() { new() { CardId = id, CountDelta = -count } } };
    }
    internal static int Playset(string id) => 2;
    internal static int MassExtras(IReadOnlyDictionary<string, int> counts, string id) => Math.Max(0, counts.GetValueOrDefault(id) - Playset(id));
}
internal sealed class EntityDef { internal string Id = "TEST_CARD"; internal string GetCardId() => Id; }
internal static class LocalCardCatalog { internal static bool IsCore(string id) => id != null && id.StartsWith("CORE_", StringComparison.OrdinalIgnoreCase); }
internal sealed class CardSetDbfRecord { internal bool IsFeaturedCardSet { get; set; } internal bool IsCoreCardSet { get; set; } }
internal static class GameUtils
{
    internal static int TranslateCardIdToDbId(string id) => 1;
    internal static IEnumerable<string> GetAllCollectibleCardIds() => new[] { "TEST_CARD" };
}
internal sealed class TestLog
{
    internal readonly List<string> Errors = new();
    internal void LogError(object error) => Errors.Add(error.ToString());
    internal void LogWarning(object error) { }
}
internal static class AccessTools
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static FieldInfo Field(Type type, string name) => type.GetField(name, Flags);
    internal static PropertyInfo Property(Type type, string name) => type.GetProperty(name, Flags);
}
internal sealed class CraftingManager
{
    internal enum CanCraftCardResult { NotCraftable, TooManyCopies, NotEnoughDust }
    internal static CraftingManager Instance = new();
    internal static CraftingManager Get() => Instance;
    internal static bool IsInitialized => true;
    internal void UpdateBankText() => NativeCrafting.Boundary("bank");
    internal CraftingPendingTransaction m_pendingClientTransaction = new(), m_pendingServerTransaction = new();
    internal long DustAdjustment = -80;
    // Native nets the shown quality's pending crafts against its sales.
    internal int GetNumClientTransactions() => m_pendingClientTransaction == null ? 0
        : m_pendingClientTransaction.NormalCreateCount - m_pendingClientTransaction.NormalDisenchantCount;
    internal bool IsInCraftingMode { get; set; } = true;
    internal int Notifications;
    internal bool Clean => m_pendingClientTransaction == null && m_pendingServerTransaction == null && DustAdjustment == 0;
    internal void ResetUnCommitedArcaneDustChanges() => DustAdjustment = 0;
    internal CraftingPendingTransaction GetPendingClientTransaction() => m_pendingClientTransaction;
    internal void OnCardCreated(Network.CardSaleResult sale) { NativeCrafting.Boundary("callback"); Notifications++; }
    internal void OnCardDisenchanted(Network.CardSaleResult sale) { NativeCrafting.Boundary("callback"); Notifications++; LastSalePremium = sale.Premium; }
    internal void OnCardUpgraded(Network.CardSaleResult sale) { NativeCrafting.Boundary("callback"); Notifications++; LastSalePremium = sale.Premium; LastSaleAction = sale.Action; }
    internal Network.CardSaleResult.SaleResult LastSaleAction;
    internal TAG_PREMIUM LastSalePremium;
    internal EntityDef Shown; internal TAG_PREMIUM ShownPremium;
    internal bool GetShownCardInfo(out EntityDef entityDef, out TAG_PREMIUM premium)
    { entityDef = Shown; premium = ShownPremium; return Shown != null; }
}
internal sealed class CollectionManager
{
    internal sealed class CardModification
    {
        internal enum ModificationType { Add, Remove }
        internal string cardID;
        internal TAG_PREMIUM premium;
        internal int count;
        internal bool seenBefore;
        internal ModificationType modificationType;
    }
    internal readonly Dictionary<(string, TAG_PREMIUM), int> PremiumCounts = new();
    internal List<CardModification> LastChanges = new();
    internal int GetNumCopiesInCollection(string id, TAG_PREMIUM premium)
    {
        // Count synchronization reads separately from the injected OnCardsModified failure.
        NativeCrafting.Attempts["sync"] = NativeCrafting.Attempts.GetValueOrDefault("sync") + 1;
        return premium == TAG_PREMIUM.NORMAL ? Counts.GetValueOrDefault(id) : PremiumCounts.GetValueOrDefault((id, premium));
    }
    internal void OnCardsModified(List<CardModification> changes)
    {
        LastChanges = changes;
        NativeCrafting.Boundary("modify");
        if (NativeCrafting.Failures.Contains("sync")) throw new Exception("sync failure");
        foreach (var change in changes)
        {
            int delta = change.modificationType == CardModification.ModificationType.Add ? change.count : -change.count;
            if (change.premium == TAG_PREMIUM.NORMAL) Counts[change.cardID] = Counts.GetValueOrDefault(change.cardID) + delta;
            else PremiumCounts[(change.cardID, change.premium)] = PremiumCounts.GetValueOrDefault((change.cardID, change.premium)) + delta;
        }
    }
    internal CollectionDisplay GetCollectibleDisplay() => new();
    internal delegate void OnMassDisenchant(int amount);
    internal static CollectionManager Instance = new();
    internal static CollectionManager Get() => Instance;
    internal readonly Dictionary<string, int> Counts = new();
    internal List<OnMassDisenchant> m_massDisenchantListeners = new();
    private void OnMassDisenchantResponse() => NativeCrafting.NotifyMassListeners(this);
}
internal sealed class TestButton {
    internal bool Enabled = false;
    internal void SetEnabled(bool enabled) => Enabled = enabled;
    internal bool Throws;
    internal void DisableButton() { if (Throws) throw new MissingMethodException("CraftingButton", "DisableButton"); Enabled = false; }
}
public enum LocalTokenRarity { Common, Rare, Epic, Legendary }
public sealed class LocalCardTokens { public int Common, Rare, Epic, Legendary; }
internal sealed class CraftingUI
{
    internal readonly TestButton m_buttonCreate = new();
    internal readonly UberText m_craftValue = new(); // NativeHomeTests' UberText
    internal bool CreateCostBar = true;
    private void SetCreateCostBarActive(bool active) => CreateCostBar = active;
    internal readonly TestButton m_buttonDisenchant = new();
    internal bool DisenchantCostBar = true;
    private void SetDisenchantCostBarActive(bool active) => DisenchantCostBar = active;
}
internal sealed class MassDisenchant
{
    internal static MassDisenchant Instance = new();
    internal static MassDisenchant Get() => Instance;
    internal readonly TestButton m_disenchantButton = new();
    internal bool Blocked = true;
    private void BlockUI(bool block) { Blocked = block; NativeCrafting.NotifyUnblocked(block); }
}
internal sealed class CollectibleCard
{
    internal string CardId;
    internal TAG_PREMIUM PremiumType;
    // Mirrors native: IsCraftableDisenchantCount = max(OwnedCount - DefaultMaxCopiesPerDeck, 0).
    public int OwnedCount { get; set; }
    public int DefaultMaxCopiesPerDeck => 2;
    internal int IsCraftableDisenchantCount => Math.Max(OwnedCount - DefaultMaxCopiesPerDeck, 0);
}
internal static class Network
{
    internal sealed class MassDisenchantResponse { internal int Amount; }
    internal sealed class CardSaleResult
    {
        internal enum SaleResult { CARD_WAS_BOUGHT, CARD_WAS_SOLD, CARD_WAS_UPGRADED }
        internal int AssetID;
        internal string AssetName;
        internal TAG_PREMIUM Premium;
        internal SaleResult Action;
    }
}

internal sealed class CollectionDisplay
{
    internal PageManager GetPageManager() => new();
    internal void UpdateCurrentPageCardLocks() => NativeCrafting.Boundary("locks");
}
internal sealed class PageManager
{
    internal void RefreshCurrentPageContents() => NativeCrafting.Boundary("page");
}
internal sealed class ArcaneDustAmount
{
    internal void UpdateCurrentDustAmount() => NativeCrafting.Boundary("display");
}
namespace UnityEngine
{
    internal enum FindObjectsSortMode { None }
    internal static class Object
    {
        internal static T[] FindObjectsByType<T>(FindObjectsSortMode mode) =>
            new[] { (T)(object)new ArcaneDustAmount(), (T)(object)new ArcaneDustAmount() };
    }
}
internal sealed class AlertPopup
{
    internal enum ResponseDisplay { OK }
    internal sealed class PopupInfo
    {
        internal string m_headerText, m_text;
        internal bool m_showAlertIcon;
        internal ResponseDisplay m_responseDisplay;
    }
}
internal sealed class DialogManager
{
    internal static DialogManager Get() => new();
    internal void ShowPopup(AlertPopup.PopupInfo info) => NativeCrafting.Popups.Add(info.m_text);
}
internal sealed class CardValueDbfRecord { }
internal enum EventTimingType { SPECIAL_EVENT_NEVER }
internal static class NetCache
{
    internal sealed class NetCacheArcaneDustBalance { internal long Balance; }
    internal sealed class CardValue
    {
        internal int BaseBuyValue, BaseSellValue, BaseUpgradeValue;
        internal EventTimingType OverrideEvent;
    }
}
