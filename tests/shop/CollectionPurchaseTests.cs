using System.Xml.Serialization;

partial class NativeBundleHarness
{
    private static class Assets { internal static class CardHero { internal enum PortraitCurrency { UNKNOWN, GOLD, REAL_MONEY } } }
    private static class HeroSkinUtils {
        internal struct HeroSkinProductData { public int ProductId; public Assets.CardHero.PortraitCurrency CurrencyType; public bool IsDelayedRelease; }
    }
    private sealed class EntityDef { internal string CardId; internal string GetCardId() => CardId; }
    private class CollectionPreview {
        internal bool Active, Visible;
        internal int? CardBack;
        internal EntityDef Entity;
        internal int Failures;
        internal string BrokenRead;
        internal void Ack(object bundle, PaymentMethod method) {
            Active = false; Visible = true;
            var store = StoreManager.Get();
            foreach (string name in store.Listeners.Keys.ToArray())
                store.Listeners[name] = Delegate.Remove(store.Listeners[name], (Action<object, PaymentMethod>)Ack);
        }
    }
    private sealed class CardBackInfoManager : CollectionPreview {
        internal static CardBackInfoManager Instance;
        internal static bool ThrowOnGet;
        internal static CardBackInfoManager Get() => ThrowOnGet ? throw new IOException("card-back caller unavailable") : Instance;
    }
    private sealed class HeroSkinInfoManager : CollectionPreview {
        internal static HeroSkinInfoManager Instance;
        internal static bool ThrowOnGet;
        internal static HeroSkinInfoManager Get() => ThrowOnGet ? throw new IOException("hero-skin caller unavailable") : Instance;
    }
    internal static void RunCollectionMapping(Action<bool, string> check)
    {
        foreach (var product in NativeShopPurchase.Products.Where(p => p.Kind is "CardBack" or "HeroSkin")) {
            int id = product.Kind == "CardBack" ? NativeShopPurchase.CollectionCardBackProductId(product.CardBack.Value)
                : NativeShopPurchase.CollectionHeroSkinProductId(product.HeroSkin);
            check(id == NativeShopPurchase.PmtId(product), "Collection maps every offered cosmetic " + product.Id);
            if (product.Kind == "HeroSkin") {
                var data = new HeroSkinUtils.HeroSkinProductData();
                check(!CollectionHeroSkinProduct(product.HeroSkin, ref data) && data.ProductId == id
                    && data.CurrencyType == Assets.CardHero.PortraitCurrency.GOLD && !data.IsDelayedRelease, "Collection hero GOLD lookup");
            } else {
                int result = -1;
                check(!CollectionCardBackProduct(product.CardBack.Value, ref result) && result == id, "Collection card-back prefix");
            }
        }
        foreach (string skin in LocalClassRewards.Portraits.Select(p => p.Honored)
            .Concat(AchievementExclusiveCosmetics.All.Where(p => p.Kind == "HeroSkin").Select(p => p.HeroSkinId))
            .Concat(LocalShop.Default.Products.Where(p => p.Kind == "HeroSkin" && p.verify).Select(p => p.HeroSkin))
            .Concat(new[] { "UNKNOWN_HERO", "hero_08a", "", null })) {
            var data = new HeroSkinUtils.HeroSkinProductData { ProductId = 123, CurrencyType = Assets.CardHero.PortraitCurrency.REAL_MONEY, IsDelayedRelease = true };
            check(NativeShopPurchase.CollectionHeroSkinProductId(skin) == 0 && !CollectionHeroSkinProduct(skin, ref data)
                && data.ProductId == 0 && data.CurrencyType == Assets.CardHero.PortraitCurrency.UNKNOWN && !data.IsDelayedRelease,
                "Collection excludes non-offered hero " + skin);
        }
        foreach (int back in AchievementExclusiveCosmetics.All.Where(p => p.Kind == "CardBack").Select(p => p.CardBackId)
            .Concat(new[] { 32, 232, 94, 153, 5, 553, 500, 0, -1, int.MaxValue })
            .Concat(LocalShop.Default.Products.Where(p => p.Kind == "CardBack" && p.verify).Select(p => p.CardBack.Value))) {
            int result = 123;
            check(NativeShopPurchase.CollectionCardBackProductId(back) == 0 && !CollectionCardBackProduct(back, ref result) && result == 0,
                "Collection excludes non-offered card back " + back);
        }
        var offered = NativeShopPurchase.Products.First(p => p.Kind == "HeroSkin");
        NativeShopPurchase.Seed(p => p.Id != offered.Id, null);
        check(NativeShopPurchase.CollectionHeroSkinProductId(offered.HeroSkin) == 0, "runtime-excluded cosmetic has no Collection offer");
        NativeShopPurchase.Seed(null, null);
        foreach (string kind in new[] { "CardBack", "HeroSkin", "Pack", "Deck", "Recipe", "Bundle", "Pet", "Board", "HubTheme" }) {
            check(NativeShopPurchase.AllowsClosedShop(kind, "same", "same") == (kind is "CardBack" or "HeroSkin"), "closed Shop kind gate " + kind);
            check(!NativeShopPurchase.AllowsClosedShop(kind, "different", "same") && !NativeShopPurchase.AllowsClosedShop(kind, null, "same")
                && !NativeShopPurchase.AllowsClosedShop(kind, "", ""), "closed Shop requires matching Collection context " + kind);
        }
        foreach (string mode in new[] { "disabled", "unready", "retail" }) {
            Enabled = mode != "disabled"; ready = mode != "unready"; Isolated = mode != "retail";
            var data = new HeroSkinUtils.HeroSkinProductData { ProductId = 123 };
            int back = 123;
            bool heroOriginal = CollectionHeroSkinProduct(offered.HeroSkin, ref data), backOriginal = CollectionCardBackProduct(1, ref back);
            check(mode == "retail" ? heroOriginal && backOriginal && data.ProductId == 123 && back == 123
                : !heroOriginal && !backOriginal && data.ProductId == 0 && back == 0 && data.CurrencyType == Assets.CardHero.PortraitCurrency.UNKNOWN,
                "Collection prefix isolation/readiness " + mode);
        }
        Isolated = Enabled = ready = true;
        string root = RepoRoot.Find("plugin/NativeShop.cs");
        string shop = File.ReadAllText(Path.Combine(root, "plugin/NativeShop.cs"));
        string probe = File.ReadAllText(Path.Combine(root, "plugin/NativeUIProbe.cs"));
        foreach (string flag in new[] { "BuyCardBacksFromCollectionManager", "BuyHeroSkinsFromCollectionManager", "LargeItemBundleDetailsEnabled" })
            check(shop.Contains("features.Store." + flag + " = true;"), "Store flag published " + flag);
        check(probe.Contains("features.Collection.CosmeticsRenderingEnabled = false;") && !probe.Contains("CosmeticsRenderingEnabled = true"), "BG cosmetic rendering stays false");
    }
    private static CollectionPreview Preview(LocalShopProduct product)
    {
        CollectionPreview preview;
        CardBackInfoManager.Instance = null; HeroSkinInfoManager.Instance = null;
        if (product.Kind == "CardBack") preview = CardBackInfoManager.Instance = new() { CardBack = product.CardBack };
        else preview = HeroSkinInfoManager.Instance = new() { Entity = new() { CardId = product.HeroSkin } };
        preview.Active = true;
        foreach (string name in StoreManager.Get().Listeners.Keys.ToArray())
            StoreManager.Get().Listeners[name] = Delegate.Combine(StoreManager.Get().Listeners[name], (Action<object, PaymentMethod>)preview.Ack);
        return preview;
    }
    private static string ReceiptXml(LocalPurchaseReceipt receipt)
    {
        using var writer = new StringWriter(); new XmlSerializer(typeof(LocalPurchaseReceipt)).Serialize(writer, receipt); return writer.ToString();
    }
    private static void RunCollectionPurchases(Action<bool, string> check, Func<string, int, string> fresh)
    {
        var store = StoreManager.Get();
        Shop.Open = false;
        foreach (var product in NativeShopPurchase.Products.Where(p => p.Kind is "CardBack" or "HeroSkin").GroupBy(p => p.Kind).Select(g => g.First())) {
            string path = fresh("collection-" + product.Id, 100000);
            using var profile = new LocalProfile(path); Profile = profile; products = new Service();
            foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
            long pmt = NativeShopPurchase.PmtId(product);
            int price = profile.PurchasePrice(product.Id), acks = store.Acks;
            RecoverUI();
            var preview = Preview(product);
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.REAL_MONEY, 1, true));
            check(!preview.Active && preview.Visible && preview.Failures == 1 && transaction == null && store.Acks == acks,
                "Collection preflight failure restores only caller without global ack " + product.Kind);
            check(store.Listeners.Values.All(d => d == null || d.GetInvocationList().All(l => l.Target != preview)), "refused controller listeners detached");
            preview = Preview(product); preview.Active = false;
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
            check(transaction == null && profile.PurchaseSequence == 0, "matching preview without active Collection transaction refused");
            preview.Ack(null, PaymentMethod.VIRTUAL_CURRENCY);
            preview = Preview(product);
            if (product.Kind == "CardBack") preview.CardBack++;
            else preview.Entity.CardId = "WRONG_HERO";
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
            check(!preview.Active && preview.Visible && preview.Failures == 1 && transaction == null && profile.PurchaseSequence == 0
                && store.Acks == acks, "mismatched Collection cosmetic releases caller without global ack");
            foreach (string mode in new[] { "selection-throw", "caller-get-throw", "caller-read-throw", "not-offered", "wrong-kind", "disabled", "unready", "null" }) {
                preview = Preview(product);
                if (mode == "selection-throw") preview.BrokenRead = product.Kind == "CardBack" ? "m_currentCardBackIdx" : "m_currentEntityDef";
                if (mode == "caller-get-throw") {
                    CardBackInfoManager.ThrowOnGet = product.Kind == "HeroSkin";
                    HeroSkinInfoManager.ThrowOnGet = product.Kind == "CardBack";
                }
                if (mode == "caller-read-throw") {
                    if (product.Kind == "HeroSkin") CardBackInfoManager.Instance = new() { BrokenRead = "m_isStoreTransactionActive" };
                    else HeroSkinInfoManager.Instance = new() { BrokenRead = "m_isStoreTransactionActive" };
                }
                if (mode == "not-offered") NativeShopPurchase.Seed(p => p.Id != product.Id, null);
                Enabled = mode != "disabled"; ready = mode != "unready";
                long id = mode == "wrong-kind" ? NativeShopPurchase.PmtId(NativeShopPurchase.Products.First(p => p.Kind != product.Kind && p.Kind is "CardBack" or "HeroSkin")) : pmt;
                Buy(store, mode == "null" ? null : new BuyProductEventArgs(id, CurrencyType.GOLD, 1, true));
                check(!preview.Active && preview.Visible && preview.Failures == 1 && transaction == null && profile.PurchaseSequence == 0
                    && store.Acks == acks, "Collection refusal releases caller without transaction/global ack " + product.Kind + " " + mode);
                CardBackInfoManager.ThrowOnGet = HeroSkinInfoManager.ThrowOnGet = false;
                Enabled = ready = true; NativeShopPurchase.Seed(null, null);
            }
            var dialogs = DialogManager.Get();
            foreach (string mode in new[] { "gold", "pending-match", "pending-reward", "pending-pack", "Playing", "Starting" }) {
                using var refused = new LocalProfile(fresh("collection-refusal-" + product.Id + "-" + mode, mode == "gold" ? price - 1 : 100000));
                Profile = refused;
                if (mode is "pending-match" or "pending-reward") {
                    var match = refused.Begin("mage", "warrior", 1);
                    if (mode == "pending-reward") refused.Settle(match.Id, "Lost", 1, true);
                }
                if (mode == "pending-pack") { refused.Purchase("pack-standard", 1, 100, "pending"); refused.BeginNativePack(); }
                LocalLadder.Screen = mode is "Playing" or "Starting" ? mode : "";
                int popups = dialogs.Shown.Count, gold = refused.Gold; long receipts = refused.PurchaseSequence;
                preview = Preview(product);
                dialogs.BeforeShow = () => check(!preview.Active && preview.Visible && preview.Failures == 1, "Collection released before popup " + mode);
                Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
                dialogs.BeforeShow = null;
                string text = mode == "gold" ? "GLUE_STORE_FAIL_NOT_ENOUGH_GOLD\n" + gold + " / " + price + " gold"
                    : mode == "pending-pack" ? "A native pack reveal is pending."
                    : mode is "Playing" or "Starting" ? "Match active." : "Match or rewards pending.";
                var popup = dialogs.Shown.Last();
                check(!preview.Active && preview.Failures == 1 && dialogs.Shown.Count == popups + 1 && popup.m_text == text
                    && popup.m_headerText == "GLUE_STORE_AUTH_FAIL_HEADLINE" && popup.m_responseDisplay == AlertPopup.ResponseDisplay.OK
                    && popup.m_okText == "GLOBAL_OKAY" && transaction == null && store.Acks == acks
                    && refused.Gold == gold && refused.PurchaseSequence == receipts, "Collection refusal popup " + product.Kind + " " + mode);
                preview = Preview(product); Shop.Open = true;
                CardBackInfoManager.ThrowOnGet = HeroSkinInfoManager.ThrowOnGet = true;
                Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
                check(preview.Active && !preview.Visible && preview.Failures == 0 && transaction == null && store.Acks == acks
                    && dialogs.Shown.Count == popups + 1, "Shop refusal leaves Collection untouched and shows no new popup " + mode);
                CardBackInfoManager.ThrowOnGet = HeroSkinInfoManager.ThrowOnGet = false;
                preview.Ack(null, PaymentMethod.VIRTUAL_CURRENCY); Shop.Open = false;
            }
            Profile = profile; LocalLadder.Screen = "";
            preview = Preview(product);
            var request = new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true);
            Buy(store, request);
            var pending = transaction; var pendingAuth = auth;
            int busyPopups = dialogs.Shown.Count;
            check(pending != null && preview.Active && ReferenceEquals(collectionPurchase, preview), "closed Shop Collection reaches shared auth " + product.Kind);
            Buy(store, request);
            check(ReferenceEquals(transaction, pending) && preview.Active && preview.Failures == 0 && dialogs.Shown.Count == busyPopups,
                "duplicate cannot end owning Collection transaction or show popup");
            var other = Preview(product);
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
            check(!other.Active && other.Visible && other.Failures == 1 && ReferenceEquals(transaction, pending)
                && ReferenceEquals(auth, pendingAuth) && store.Acks == acks && store.Blocked && preview.Active
                && dialogs.Shown.Count == busyPopups, "busy refusal preserves unrelated transaction/auth/listener without popup");
            var opposite = NativeShopPurchase.Products.First(p => p.Kind != product.Kind && p.Kind is "CardBack" or "HeroSkin");
            other = Preview(opposite);
            if (product.Kind == "CardBack") CardBackInfoManager.Instance = (CardBackInfoManager)preview;
            else HeroSkinInfoManager.Instance = (HeroSkinInfoManager)preview;
            Buy(store, new BuyProductEventArgs(NativeShopPurchase.PmtId(opposite), CurrencyType.GOLD, 1, true));
            check(!other.Active && other.Failures == 1 && preview.Active && ReferenceEquals(transaction, pending)
                && ReferenceEquals(auth, pendingAuth) && store.Acks == acks && dialogs.Shown.Count == busyPopups,
                "busy cross-kind refusal releases new caller and preserves Collection owner");
            profile.CommitFailure = _ => throw new IOException("Collection save failed");
            TickPurchase(); profile.CommitFailure = null;
            auth.Response(false, pending);
            check(!preview.Active && profile.Gold == 100000 && profile.PurchaseSequence == 0 && store.Acks == acks + 1,
                "Collection save failure uses existing transaction failure ack");
            preview = Preview(product);
            Buy(store, request);
            check(!preview.Active && preview.Failures == 1 && transaction == null && store.Acks == acks + 1
                && dialogs.Shown.Count == busyPopups, "completed duplicate releases new caller silently");
            preview = Preview(product);
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
            pending = transaction;
            TickPurchase();
            var receipt = purchaseReceipt;
            check(committed && receipt.ProductId == product.Id && receipt.GoldPaid == price && profile.Gold == 100000 - price
                && NativeShopPurchase.Owned(profile, product, price), "Collection commits durable debit and ownership " + product.Kind);
            using (var direct = new LocalProfile(fresh("collection-ledger-" + product.Id, 100000))) {
                var expected = direct.Purchase(product.Id, 1, price, pending.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                check(ReceiptXml(receipt) == ReceiptXml(expected), "Collection receipt equals Shop ledger receipt " + product.Kind);
            }
            byte[] saved = File.ReadAllBytes(path);
            check(new NativeShopPurchase().Commit(profile, pending.ID, pmt, "XSH", 1, price).Sequence == receipt.Sequence
                && saved.SequenceEqual(File.ReadAllBytes(path)), "Collection request-id replay has no debit/write");
            Acknowledge(store, true, pending);
            check(!preview.Active && preview.Visible && transaction == null, "Collection success ack restores owned preview");
            profile.Dispose();
            using var reopened = new LocalProfile(path);
            check(new NativeShopPurchase().Commit(reopened, pending.ID, pmt, "XSH", 1, price).Sequence == receipt.Sequence
                && reopened.Gold == 100000 - price && NativeShopPurchase.Owned(reopened, product, price), "Collection replay/ownership survive restart");
        }
        CardBackInfoManager.Instance = null; HeroSkinInfoManager.Instance = null;
        foreach (string name in store.Listeners.Keys.ToArray()) store.Listeners[name] = null;
        store.Listeners["OnFailedPurchaseAck"] = (Action<object, PaymentMethod>)((_, _) => store.Acks++);
        RecoverUI(); Shop.Open = true;
    }
}
