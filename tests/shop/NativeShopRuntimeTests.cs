global using System.Reflection;

// Production validation, availability, quote refresh and tab filtering against native model doubles.
partial class NativeBundleHarness
{
    private static LocalProfile Profile;
    private static bool Isolated = true, Enabled = true;
    private static Service products;
    private enum CurrencyType { GOLD, REAL_MONEY }
    private enum ProductAvailability { UNDEFINED, CAN_PURCHASE, ALREADY_OWNED, RESTRICTED, LOCKED }
    private enum RewardItemType { BOOSTER, CARD_BACK, HERO_SKIN, SELLABLE_DECK, ARENA_TICKET, PET, CUSTOM_COIN }
    private sealed class PriceInfo {
        internal struct Price { internal long RealPrice; internal string DisplayPrice; }
        internal Price CurrentPrice, OriginalPrice;
    }
    private sealed class PriceDataModel { internal CurrencyType Currency; internal float Amount; }
    private sealed class Booster { internal int Type; }
    private sealed class CardBack { internal int CardBackId; }
    private sealed class Card { internal string CardId; }
    private sealed class Item { internal RewardItemType ItemType; internal int Quantity, ItemId; internal Booster Booster; internal CardBack CardBack; internal Card Card; }
    private sealed class ProductDataModel {
        internal long PmtId;
        internal List<PriceDataModel> Prices = new();
        internal List<Item> Items = new();
        internal List<string> Tags = new();
        internal string Description, Name, VariantName;
        internal List<ProductDataModel> Variants;
        internal ProductAvailability Availability;
    }
    private sealed class SellableDeckProductPage { internal ProductDataModel FirstVariant; }
    // Exercise both production Harmony postfixes with every class and filtered offerings.
    private static void RunPageVariants(Action<bool, string> check)
    {
        var offered = NativeShopPurchase.Products;
        var models = offered.Select(p => new ProductDataModel { PmtId = NativeShopPurchase.PmtId(p) }).ToList();
        foreach (var product in offered)
        {
            var model = models.Single(m => m.PmtId == NativeShopPurchase.PmtId(product));
            IEnumerable<ProductDataModel> result = new[] { model };
            var unchanged = result;
            DeckVariants(model, models, ref result);
            var recipe = offered.FirstOrDefault(p => p.Kind == "Recipe" && p.RecipeId == product.RecipeId);
            var plate = NativeShopArtMath.ParseRecipePlate(null);
            if (product.Kind != "Deck" || recipe == null) {
                check(ReferenceEquals(result, unchanged), product.Id + ": only Decks with an offered Recipe get page variants");
                model.Availability = ProductAvailability.CAN_PURCHASE;
                check(RecipeTileText(model, plate) == null, product.Id + ": no recipe price plate without page variants");
                continue;
            }
            var recipeModel = models.Single(m => m.PmtId == NativeShopPurchase.PmtId(recipe));
            check(result.SequenceEqual(new[] { model, recipeModel }) && model.VariantName == "Ready-made" && recipeModel.VariantName == "Recipe",
                product.Id + ": native variants are [Deck, Recipe] with local labels");
            model.Variants = result.ToList();
            // NativeShopTilePrices: the Deck tile's recipe plate only while both variants show a price.
            model.Prices = new() { new PriceDataModel { Amount = 1500 } }; recipeModel.Prices = new() { new PriceDataModel { Amount = 100 } };
            bool priced(ProductAvailability state) => state == ProductAvailability.CAN_PURCHASE || state == ProductAvailability.LOCKED;
            foreach (var deckState in Enum.GetValues<ProductAvailability>())
                foreach (var recipeState in Enum.GetValues<ProductAvailability>()) {
                    model.Availability = deckState; recipeModel.Availability = recipeState;
                    check(RecipeTileText(model, plate) == (priced(deckState) && priced(recipeState) ? "100 Recipe" : null),
                        product.Id + ": recipe plate for deck " + deckState + ", recipe " + recipeState);
                }
            model.Availability = recipeModel.Availability = ProductAvailability.CAN_PURCHASE;
            check(RecipeTileText(model, NativeShopArtMath.ParseRecipePlate("none")) == null
                && RecipeTileText(model, NativeShopArtMath.ParseRecipePlate("text=Recipe {0}")) == "Recipe 100", product.Id + ": plate setting off / label");
            check(RecipeTileText(recipeModel, plate) == null, product.Id + ": the Recipe model never gets a second plate");
            model.Variants = new() { recipeModel, model };
            check(RecipeTileText(model, plate) == null, product.Id + ": variants not led by the Deck get no plate");
            recipeModel.Prices = new();
            model.Variants = result.ToList();
            check(RecipeTileText(model, plate) == null, product.Id + ": an unpriced Recipe model gets no plate");
            recipeModel.Prices = new() { new PriceDataModel { Amount = 100 } };
            var page = new SellableDeckProductPage();
            ProductDataModel first = recipeModel;
            model.VariantName = "native default"; recipeModel.VariantName = "native default";
            DeckFirstVariant(page, model, ref first);
            check(ReferenceEquals(first, model) && ReferenceEquals(page.FirstVariant, model)
                && model.VariantName == "Ready-made" && recipeModel.VariantName == "Recipe",
                product.Id + ": page opens on Deck and preserves variant labels");
            result = unchanged;
            DeckVariants(model, models.Where(m => m != recipeModel).ToList(), ref result);
            check(ReferenceEquals(result, unchanged), product.Id + ": missing native Recipe model leaves native variants alone");
        }
    }
    private sealed class ProductInfo {
        internal readonly Dictionary<CurrencyType, PriceInfo> Prices = new();
        internal string Description;
        internal PriceDataModel GetPriceDataModel(CurrencyType currency) => new() {
            Currency = currency, Amount = Prices[currency].CurrentPrice.RealPrice };
    }
    private sealed class ProductCatalog {
        internal static int Dirty;
        internal IEnumerable<ProductDataModel> Products => products.Models.Values;
        internal void MarkProductDataAsDirty() => Dirty++;
    }
    private sealed class Service {
        internal readonly Dictionary<long, ProductInfo> Infos = new();
        internal readonly Dictionary<long, ProductDataModel> Models = new();
        internal ProductDataModel GetProductDataModel(long pmt) => Models.TryGetValue(pmt, out var model) ? model : null;
        internal void UpdateProductStatus() => UpdateAvailability(new ProductCatalog());
        internal void SetProduct(Network.Bundle bundle) {
            long pmt = bundle.PMTProductID.Value;
            if (!Infos.TryGetValue(pmt, out var info)) Infos[pmt] = info = new ProductInfo { Description = bundle.DisplayDescription };
            // Same zero-price omission as pinned ProductInfo.UpdatePrices; production postfix repairs it.
            if (bundle.GoldCost.Value.CurrentCost > 0) {
                var price = new PriceInfo.Price { RealPrice = bundle.GoldCost.Value.CurrentCost };
                info.Prices[CurrencyType.GOLD] = new PriceInfo { CurrentPrice = price, OriginalPrice = price };
            }
            LocalPrice(info, bundle);
            if (!Models.ContainsKey(pmt)) Models[pmt] = new ProductDataModel {
                PmtId = pmt, Description = bundle.DisplayDescription,
                Prices = new() { info.GetPriceDataModel(CurrencyType.GOLD) },
                Name = bundle.DisplayName, Tags = bundle.Attributes["tags"].Split(',').ToList(),
                // Pinned RewardFactory: a DRAFT item becomes an ARENA_TICKET reward with no item id.
                Items = bundle.Items.Select(i => new Item { Quantity = i.Quantity, ItemId = i.ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_DRAFT ? 0 : i.ProductData,
                    Booster = i.ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_BOOSTER ? new Booster { Type = i.ProductData } : null,
                    CardBack = i.ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_CARD_BACK && GameDbf.CardBack.GetRecord(i.ProductData) != null ? new CardBack { CardBackId = i.ProductData } : null,
                    Card = i.ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_COSMETIC_COIN ? new Card { CardId = GameDbf.CosmeticCoin.GetRecord(i.ProductData)?.CardRecord?.NoteMiniGuid } : i.ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_HERO && GameUtils.GetCardHeroRecordForCardId(i.ProductData) != null ? new Card { CardId = GameUtils.TranslateDbIdToCardId(i.ProductData) } : null,
                    ItemType = i.ItemType switch {
                        PegasusUtil.ProductType.PRODUCT_TYPE_BOOSTER => RewardItemType.BOOSTER,
                        PegasusUtil.ProductType.PRODUCT_TYPE_CARD_BACK => RewardItemType.CARD_BACK,
                        PegasusUtil.ProductType.PRODUCT_TYPE_HERO => RewardItemType.HERO_SKIN,
                        PegasusUtil.ProductType.PRODUCT_TYPE_DRAFT => RewardItemType.ARENA_TICKET,
                        PegasusUtil.ProductType.PRODUCT_TYPE_COSMETIC_COIN => RewardItemType.CUSTOM_COIN,
                        PegasusUtil.ProductType.PRODUCT_TYPE_PET => RewardItemType.PET,
                        _ => RewardItemType.SELLABLE_DECK } }).ToList() };
        }
    }
    private sealed class ShopTabController {
        internal sealed class TabData { internal Tab ParentTabData; }
        internal int ActiveTabIndex = -1, Tabs = 1, Used;
        internal Transform transform;
        internal readonly List<object> SubTabs = new();
        internal int GetNumberOfTabs() => Tabs;
        internal int GetNumberOfActiveSubTabs() => Used;
    }
    private sealed class Tab { internal string Id; }
    private static object Read(ProductInfo info, string name) => name == "m_vcPrices" ? info.Prices : throw new Exception(name);
    private static NativeShopConfirmation confirmation = null; // Fallback local confirmation never opens in the click flow.
    // #57 deck kit question: production AskDeckKit raises a native AlertPopup; the double records the question.
    private static readonly NativeShopKitChoice kits = new();
    private static AlertPopup.PopupInfo kitQuestion;
    private static string purchaseKit;
    private static readonly List<(long Pmt, string Kit, int Price)> kitQuestions = new();
    private static void AskDeckKit(ProductDataModel product, LocalShopProduct local, LocalDeckKit kit, int price)
    { kits.Ask(product.PmtId, price, kit); kitQuestions.Add((product.PmtId, kit.Id, price)); }
    // Mirrors pinned PurchaseProduct -> Internal_PurchaseProduct -> Internal_PurchaseProductCoroutine, with the shop hooks
    // where Harmony installs them. The coroutine runs synchronously until its first yield (the GOLD confirmation popup).
    private sealed class PurchaseManager {
        internal struct PurchaseManagerOptions { internal bool SuppressVcConfirmation; }
        internal sealed class PurchaseOrder { internal ProductDataModel Product; internal PriceDataModel Price; internal int Quantity = 1; internal bool HasFinished; }
        internal object Current;
        internal int Calls, Started, Confirmations, Cancelled, Bought;
        internal void PurchaseProduct(ProductDataModel product, PriceDataModel price, int quantity = 1) {
            Calls++;
            var options = new PurchaseManagerOptions();
            if (!LocalPurchase(product, price, ref options, quantity)) return;
            if (Current != null || quantity <= 0) return; // "A purchase order has already been executed"
            if (price.Amount * quantity > Profile.Gold) return; // gold has no prerequisite currency product: silent stop
            Started++;
            var order = new PurchaseOrder { Product = product, Price = price, Quantity = quantity };
            System.Collections.IEnumerator routine = null;
            bool original = GuardPurchase(this, order, ref routine);
            if (!order.HasFinished) {
                if (original) {
                    if (!options.SuppressVcConfirmation) Confirmations++; // CreatePurchaseConfirmPopup, then WaitUntil
                    else { order.HasFinished = true; Current = null; Bought++; Buy(StoreManager.Get(), new BuyProductEventArgs(product.PmtId, price.Currency, quantity, true)); }
                }
                Current = "coroutine handle"; // StartCoroutine's result is stored even when the routine already finished
            }
            ClearFinishedPurchase(this, order);
        }
    }
    private sealed class BrowserButton { internal ProductDataModel DisplayProduct; }
    // Pinned ShopSlot.m_clickable (Hearthstone.UI.Clickable: m_dragged, m_pegUiElement), its PegUIElement drag tolerance,
    // PegUI's dpi factor and InputCollection's cursor.
    private struct Point { internal float x, y; }
    private sealed class PegUIElement { internal Point Tolerance = new() { x = 5, y = 5 }; internal Point GetDragTolerance() => Tolerance; }
    private sealed class Clickable { internal bool m_dragged; internal readonly PegUIElement Peg = new(); }
    private sealed class ShopSlot {
        internal BrowserButton BrowserButtonDataModel; internal Clickable Clickable = new(); internal bool Broken;
        internal bool IsEmpty, Enabled = true; internal void SetEnabled(bool on) => Enabled = on;
    }
    private sealed class PegUI { internal static readonly PegUI Instance = new(); internal static PegUI Get() => Instance; internal float Dpi = 2; }
    private static class InputCollection {
        internal static Point Mouse; internal static bool Held;
        internal static Point GetMousePosition() => Mouse;
        internal static bool GetMouseButton(int button) => button == 0 && Held;
    }
    private static object dragReleaseClickable, pressClickable;
    private static int dragReleaseFrame = -1;
    private static float pressX, pressY;
    private static int pressFrame = -1, pressUpFrame = -1;
    private static float purchaseWindowAt = float.NegativeInfinity;
    private static readonly NativeShopRefreshState priceRefresh = new();
    private static class ServiceManager {
        internal static PurchaseManager Manager;
        internal static bool TryGet<T>(out T service) where T : class { service = Manager as T; return service != null; }
    }
    private sealed class AlertPopup {
        internal enum ResponseDisplay { OK }
        internal sealed class PopupInfo { internal string m_id, m_headerText, m_text, m_okText; internal bool m_showAlertIcon; internal ResponseDisplay m_responseDisplay; }
    }
    private sealed class DialogManager {
        private static readonly DialogManager Instance = new();
        internal static DialogManager Get() => Instance;
        internal readonly List<AlertPopup.PopupInfo> Shown = new();
        internal Action BeforeShow;
        internal bool ShowUniquePopup(AlertPopup.PopupInfo info) { BeforeShow?.Invoke(); Shown.Add(info); return true; }
    }
    private static class GameStrings { internal static string Get(string key) => key; }
    // Production ThemeNotice asks HubTheme whether the box takes the theme live; here it only records the request.
    private static readonly List<string> themeNotices = new();
    private static void ThemeNotice(string key) => themeNotices.Add(key);
    private static string themeNoticeAfterPurchase;
    private static object Read(object obj, string name) => obj switch {
        PurchaseManager.PurchaseOrder order => name switch {
            "Product" => order.Product, "Price" => order.Price, "Quantity" => order.Quantity, "HasFinished" => order.HasFinished,
            _ => throw new Exception(name) },
        PurchaseManager manager when name == "m_currentPurchaseCoroutine" => manager.Current,
        ShopTabController controller when name == "m_subTabs" => controller.SubTabs,
        ShopSlot slot when name == "m_clickable" => slot.Broken ? throw new InvalidOperationException("tile binding broken") : slot.Clickable,
        Clickable clickable when name == "m_dragged" => clickable.m_dragged,
        Clickable clickable when name == "m_pegUiElement" => clickable.Peg,
        PegUI peg when name == "m_dragToleranceDpiAdjustmentFactor" => peg.Dpi,
        CollectionPreview preview when preview.BrokenRead == name => throw new IOException("Collection binding unavailable: " + name),
        CollectionPreview preview => name switch {
            "m_isStoreTransactionActive" => preview.Active,
            "m_currentCardBackIdx" => preview.CardBack,
            "m_currentEntityDef" => preview.Entity,
            _ => throw new Exception(name) },
        _ => throw new Exception(name)
    };
    private static void Write(object obj, string name, object value) {
        if (obj is PurchaseManager.PurchaseOrder order && name == "HasFinished") order.HasFinished = (bool)value;
        else if (obj is PurchaseManager manager && name == "m_currentPurchaseCoroutine") manager.Current = value;
        else if (obj is StoreManager store) {
            if (name == "m_activeTransaction") store.Active = (Transaction)value;
            else if (name == "m_pendingProductPurchaseArgs") store.Pending = value;
            else if (store.Listeners.ContainsKey(name)) store.Listeners[name] = (Delegate)value;
            else throw new Exception(name);
        }
        else if (obj is CollectionPreview preview && name == "m_isStoreTransactionActive") preview.Active = (bool)value;
        else if (obj is SellableDeckProductPage page && name == "m_firstVariant") page.FirstVariant = (ProductDataModel)value;
        else if (name == "Description") ((ProductInfo)obj).Description = (string)value;
        else if (name != "m_hasUpdatedProductStatusOnce") throw new Exception(name);
    }
    private static object Call(Service service, string method, Network.Bundle bundle) {
        if (method != "SetProduct") throw new Exception(method);
        service.SetProduct(bundle); return true;
    }
    private static ProductInfo FindInfo(long pmt) => products.Infos[pmt];
    private static string Prerequisite() => null;
    private static string EvaluatePrerequisite() => null;
    private static readonly List<string> errors = new();
    private static void Fail(string step, Exception error) => errors.Add(step);
    private static readonly List<string> refusals = new();
    private static void Refuse(string reason) => refusals.Add(reason);
    private static void Trace(string reason) { }
    private static void RecoverStep(string name, Action action) { try { action(); } catch (Exception e) { Fail(name, e); } }
    private static void RecoverUI() {
        auth?.Hide(); auth = null; transaction = null; collectionPurchase = null;
        var store = StoreManager.Get(); store.Active = null; store.Pending = null; store.Blocked = false;
    }
    private static void RefreshWallet() { }
    private static bool ready = true, committed, successSent, resultShown;
    private static long sequence, purchasePrice;
    private static float purchaseBegan;
    private static string purchaseFailure;
    private static LocalPurchaseReceipt purchaseReceipt;
    private static readonly NativeShopPurchase purchases = new();
    private static readonly HashSet<BuyProductEventArgs> requests = new();
    private static Transaction transaction;
    private static object collectionPurchase;
    private static PurchaseAuthWindow auth;
    private const float Timeout = 30;
    private static class Time { internal static float realtimeSinceStartup = 10; internal static int frameCount; }
    private static class LocalLadder { internal static string Screen = "Menu"; }
    private static class NativeUIProbe { internal static void RefreshShopPurchase(LocalPurchaseReceipt receipt) { } internal static void RefreshPets() { } internal static bool PetEntryValid(LocalShopProduct product) => true; }
    private static class PegasusShared { internal enum BattlePayProvider { BP_PROVIDER_BLIZZARD } }
    private enum PaymentMethod { VIRTUAL_CURRENCY }
    private sealed class Transaction {
        internal long ID; internal long? PMTProductID;
        internal Transaction(long id, long pmt, PegasusShared.BattlePayProvider provider) { ID = id; PMTProductID = pmt; }
    }
    private sealed class BuyProductEventArgs {
        internal long PmtProductId; internal CurrencyType PaymentCurrency; internal int Quantity;
        internal BuyProductEventArgs(long id, CurrencyType currency, int quantity, bool show) { PmtProductId = id; PaymentCurrency = currency; Quantity = quantity; }
    }
    private sealed class Shop { internal static bool Open = true; internal static Shop Get() => new(); internal bool IsOpen() => Open; }
    private sealed class StoreManager {
        internal static readonly StoreManager Instance = new();
        internal static StoreManager Get() => Instance;
        internal Transaction Active; internal object Pending;
        internal bool Blocked, ThrowOnBlock;
        internal int OrphanPopups, Failures, Acks;
        internal readonly Dictionary<string, Delegate> Listeners = new() {
            ["OnSuccessfulPurchase"] = null, ["OnSuccessfulPurchaseAck"] = null, ["OnFailedPurchaseAck"] = null };
        internal StoreManager() { Listeners["OnFailedPurchaseAck"] = (Action<object, PaymentMethod>)((_, _) => Acks++); }
        internal void BlockStoreInterface() { Blocked = true; if (ThrowOnBlock) throw new IOException("start failed"); }
        internal void UnblockStoreInterface() => Blocked = false;
    }
    private sealed class PurchaseAuthWindow {
        internal bool IsLoaded = true, IsShown;
        internal Action<bool, Transaction> Response;
        internal void InitializeOnce(Action<bool, Transaction> response, Action cancel, Action exit) => Response = response;
        internal void Show(Transaction tx, bool external) => IsShown = true;
        internal void Hide() => IsShown = false;
    }
    private sealed class ShopWindowController {
        internal static ShopWindowController Get() => new();
        internal T GetWindow<T>() where T : new() => new();
    }
    private static readonly Dictionary<string, Lazy<System.Reflection.MemberInfo>> bindings = new();
    private static System.Reflection.MethodInfo Method(object type, string name) => throw new Exception("unexpected success dispatch");
    private static void EnumValue(object instance, string name, string value) { }
    private static object Call(object instance, string name, params object[] args) {
        if (instance is CollectionPreview preview && name == "OnFailedPurchaseAck") {
            if (preview.Active) throw new Exception("refusal must bypass global shop-window cleanup");
            preview.Failures++; preview.Visible = true;
        }
        if (name == "CancelCurrentPurchase") { var manager = (PurchaseManager)instance; manager.Cancelled++; manager.Current = null; }
        if (name == "HandleCommercePurchaseFailure") {
            var store = (StoreManager)instance; store.Failures++;
            if (store.Active == null || auth == null || !auth.IsShown) store.OrphanPopups++;
            else resultShown = true;
        }
        return null;
    }
    private static object Read(StoreManager store, string name) => name switch {
        "m_transactionIDsConclusivelyHandled" => new HashSet<long>(),
        "OnFailedPurchaseAck" or "OnSuccessfulPurchaseAck" or "OnSuccessfulPurchase" => store.Listeners[name],
        _ => null
    };
    private sealed class DbfTable {
        internal readonly HashSet<int> Missing = new();
        internal DbfRecord GetRecord(int id) => id > 0 && !Missing.Contains(id) ? new DbfRecord() : null;
    }
    private sealed class DbfRecord { internal DbfRecord CardRecord => this; internal string NoteMiniGuid => "DMF_COIN1"; }
    private static class GameDbf {
        internal static readonly DbfTable Card = new(), CardBack = new(), CosmeticCoin = new(), SellableDeck = new(), Booster = new(), Board = new();
    }
    internal static void RunCoinPurchase(Action<bool, string> check, Func<string, int, string> fresh)
    {
        using var profile = new LocalProfile(fresh("native-coin-click", 800));
        Reseed(profile);
        ServiceManager.Manager = new PurchaseManager();
        var coin = LocalShop.Default.Product("coin-3");
        var (details, manager) = Click(coin);
        check(details && transaction != null && profile.Gold == 800, "coin details Buy starts native transaction without early debit");
        var tx = transaction;
        Click(coin);
        check(transaction == tx && manager.Started == 1, "pending coin native click cannot queue a second order");
        TickPurchase(); TickPurchase();
        check(profile.Gold == 0 && profile.OwnsCoin(3) && profile.PurchaseSequence == 1 && profile.Purchases.Single().Granted.Coin == 3,
            "coin Buy/TickPurchase commits exactly one ledger grant");
        var response = auth.Response; response(true, tx); response(true, tx);
        check(transaction == null && auth == null && !StoreManager.Get().Blocked && profile.PurchaseSequence == 1,
            "coin native acknowledge clears once without replaying grant");
        Profile = null;
    }
    internal static void RunCoins(Action<bool, string> check, Action<Action, string> reject, LocalProfile profile)
    {
        Profile = profile; products = new Service();
        NativeShopPurchase.Seed(null, null);
        var coin = LocalShop.Default.Product("coin-2");
        long pmt = NativeShopPurchase.PmtId(coin);
        check(pmt == 1980000002 && NativeShopPurchase.IsLocalPmtId(pmt)
            && !NativeShopPurchase.IsLocalPmtId(1980000001), "coin PMT uses cosmetic id and excludes default");
        reject(() => NativeShopPurchase.PmtId(new LocalShopProduct { Kind = "Coin", Coin = 1 }), "default coin cannot get PMT");
        var bundle = BuildBundle(coin, 300);
        check(bundle.Items.Single().ItemType == PegasusUtil.ProductType.PRODUCT_TYPE_COSMETIC_COIN
            && bundle.Items.Single().ProductData == 2, "coin native bundle uses cosmetic id, not card dbf id");
        products.SetProduct(bundle);
        var model = products.GetProductDataModel(pmt);
        check(LocalModel(model) && model.Items.Single().ItemType == RewardItemType.CUSTOM_COIN, "coin native reward model validated");
        model.Items[0].Card.CardId = "GAME_005";
        check(!LocalModel(model), "coin wrong card art rejected");
        check(NativeEntryValid(coin), "coin DBF and card record required");
        GameDbf.CosmeticCoin.Missing.Add(2);
        check(!NativeEntryValid(coin), "unknown native coin excluded");
        GameDbf.CosmeticCoin.Missing.Clear();
        var sections = BuildSections();
        check(sections.Any(section => section.InternalName == "coin_list")
            && sections.Any(section => section.InternalName == "local-coins" && section.ProductOrder.Any(row => row.Item1 == pmt)), "coin subtab and section beside backs");
        check(NativeShopPurchase.Owned(profile, coin, 300) && NativeShopTabs.OpenTab(new[] { "Coin" }) == NativeShopTabs.Skins, "coin native ownership and skin category");
        Profile = null;
    }
    internal static void CheckGrantOnly(Action<bool, string> check, LocalProfile profile, LocalShopProduct product)
    {
        Profile = profile; products = new Service();
        foreach (var row in NativeShopPurchase.Products) products.SetProduct(BuildBundle(row, profile.PurchasePrice(row.Id)));
        RefreshPrices(); UpdateAvailability(new ProductCatalog());
        var model = products.GetProductDataModel(NativeShopPurchase.PmtId(product));
        check(model.Availability == ProductAvailability.LOCKED && model.Description.EndsWith("\n" + LocalShop.GrantOnlyDeckMessage, StringComparison.Ordinal),
            "native deck remains visible, locked, with welcome-gift refusal reason");
        check(NativeShopKitChoice.Offer(profile, product, profile.PurchasePrice(product.Id)) == null,
            "blocked deck offers no kit redemption");
        Profile = null;
    }
    internal static void RunRuntime(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        using var profile = new LocalProfile(fresh("native-models", 100000));
        Profile = profile; products = new Service();
        foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
        var catalog = new ProductCatalog(); UpdateAvailability(catalog);
        check(catalog.Products.All(p => ValidProduct(p) && p.Availability == (NativeShopPurchase.Product(p.PmtId).Price == 0
            && NativeShopPurchase.IsSelectable(NativeShopPurchase.Product(p.PmtId)) ? ProductAvailability.ALREADY_OWNED : ProductAvailability.CAN_PURCHASE)),
            "initial availability for every kind; free themes and boards are owned");
        // Identity checks run per UI element and per tile; they must never reach ledger pricing or ownership.
        Profile = null;
        check(catalog.Products.All(LocalModel) && !catalog.Products.Any(ValidProduct), "native identity holds without any ledger quote");
        Profile = profile;
        var guard = new NativeShopPurchase(); long transaction = 400;
        foreach (string kind in new[] { "Pack", "Recipe", "Deck", "HeroSkin", "CardBack", "HubTheme", "Board", "Pet" }) {
            var p = NativeShopPurchase.Products.First(p => p.Kind == kind && !(NativeShopPurchase.IsSelectable(p) && p.Price == 0));
            guard.Commit(profile, ++transaction, NativeShopPurchase.PmtId(p), "XSH", 1, profile.PurchasePrice(p.Id));
            RefreshPrices(); UpdateAvailability(catalog);
            var model = products.GetProductDataModel(NativeShopPurchase.PmtId(p));
            check(model.Availability == (kind == "Pack" ? ProductAvailability.CAN_PURCHASE : ProductAvailability.ALREADY_OWNED), "native Owned state " + kind);
            check(catalog.Products.All(m => m.Prices[0].Amount == profile.PurchasePrice(NativeShopPurchase.Product(m.PmtId).Id)), "all native prices rebuilt after " + kind);
        }
        var deck = NativeShopPurchase.Products.First(p => p.Kind == "Deck");
        var ownedDeck = products.GetProductDataModel(NativeShopPurchase.PmtId(deck));
        check(ownedDeck.Prices[0].Amount == 0 && ownedDeck.Description.Contains("\nYou own all "), "zero quote and description refreshed (bought deck owns every card)");
        products = new Service();
        foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
        UpdateAvailability(catalog);
        check(products.GetProductDataModel(NativeShopPurchase.PmtId(deck)).Availability == ProductAvailability.ALREADY_OWNED, "owned zero-gold deck seeded on restart");
        var broken = products.GetProductDataModel(NativeShopPurchase.PmtId(deck));
        broken.Prices[0].Amount++; UpdateAvailability(catalog);
        check(broken.Availability == ProductAvailability.RESTRICTED, "stale quote restricted");
        check(LocalModel(broken) && !ValidProduct(broken), "stale quote keeps native identity but not purchase validity");
        RefreshPrices(); broken.Items[0].ItemId++; UpdateAvailability(catalog);
        check(broken.Availability == ProductAvailability.RESTRICTED, "wrong native item restricted");
        check(!LocalModel(broken), "wrong native item loses identity");
        var tabs = new List<ShopTabController.TabData> {
            new() { ParentTabData = new Tab { Id = "runestones" } },
            new() { ParentTabData = new Tab { Id = "cards" } },
            new() { ParentTabData = new Tab { Id = "decks_1" } }, new() { ParentTabData = new Tab { Id = "decks_2" } },
            new() { ParentTabData = new Tab { Id = "featured" } }, new() { ParentTabData = new Tab { Id = "skins" } },
            new() { ParentTabData = new Tab { Id = "themes" } }, new() { ParentTabData = new Tab { Id = "solostone" } } };
        Isolated = false; LocalTabs(ref tabs); check(tabs.Count == 8, "retail tabs untouched outside isolation");
        Isolated = true; LocalTabs(ref tabs); check(tabs.Select(t => t.ParentTabData.Id).SequenceEqual(new[] { "cards", "decks_1", "decks_2", "skins", "themes" }), "five local categories survive filtering; foreign tabs hidden");
        foreach (string kind in new[] { "CardBack", "HeroSkin" }) {
            var cosmetic = NativeShopPurchase.Products.First(p => p.Kind == kind);
            var model = products.GetProductDataModel(NativeShopPurchase.PmtId(cosmetic));
            var itemType = model.Items[0].ItemType;
            model.Items[0].ItemType = RewardItemType.BOOSTER;
            check(!ValidProduct(model), "wrong cosmetic reward type rejected " + kind);
            model.Items[0].ItemType = itemType;
            if (kind == "CardBack") model.Items[0].CardBack.CardBackId++;
            else model.Items[0].Card.CardId = "WRONG_HERO";
            check(!ValidProduct(model), "wrong cosmetic identity rejected " + kind);
        }
        var skin = NativeShopPurchase.Products.First(p => p.Kind == "HeroSkin");
        int skinId = NativeShopPurchase.NativeItemId(skin), before = NativeShopPurchase.Products.Count;
        GameUtils.MissingHeroes.Add(skinId);
        var rejected = new List<string>(); NativeShopPurchase.Seed(NativeEntryValid, rejected.Add);
        check(NativeShopPurchase.Products.Count == before - 1 && rejected.Count == 1, "missing CARD_HERO excludes only failing cosmetic");
        products = new Service();
        foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
        UpdateAvailability(catalog);
        check(catalog.Products.All(ValidProduct), "remaining catalogue valid with failing hero");
        RefreshPrices();
        GameUtils.MissingHeroes.Clear(); NativeShopPurchase.Seed(null, rejected.Add);
        int failuresBefore = errors.Count;
        List<ShopTabController.TabData> malformedTabs = null; LocalTabs(ref malformedTabs);
        LocalPrice(new ProductInfo(), new Network.Bundle { PMTProductID = NativeShopPurchase.ProductId });
        check(errors.Count == failuresBefore + 2, "new postfix exceptions logged without escaping client");
        RunBuy(check, fresh);
        RunDeckCompletion(check, fresh);
        RunCollectionPurchases(check, fresh);
        RunClicks(check, fresh);
        RunKits(check, fresh);
        RunControlCache(check);
        Profile = null;
    }
    private static void RunBuy(Action<bool, string> check, Func<string, int, string> fresh)
    {
        string path = fresh("native-buy-refusals", 100000);
        using var profile = new LocalProfile(path);
        Profile = profile; products = new Service();
        foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
        var store = StoreManager.Get();
        var recipe = NativeShopPurchase.Products.First(p => p.Kind == "Recipe");
        profile.Purchase(recipe.Id, 1, profile.PurchasePrice(recipe.Id), "owned");
        var deck = NativeShopPurchase.Products.First(p => p.Kind == "Deck");
        long deckPmt = NativeShopPurchase.PmtId(deck);
        foreach (string mode in new[] { "owned", "stale", "invalid", "pending-pack", "pending-reward", "deck-limit", "after-start", "save-failure", "reward-after-start", "pack-after-start" }) {
            RecoverUI(); RefreshPrices();
            long pmt = mode == "owned" ? NativeShopPurchase.PmtId(recipe) : mode == "invalid" ? 123 : deckPmt;
            if (mode == "stale") products.GetProductDataModel(pmt).Prices[0].Amount++;
            if (mode == "pending-pack") { profile.Purchase("pack-standard", 1, 100, "pending"); profile.BeginNativePack(); }
            if (mode == "pending-reward") { var match = profile.Begin("mage", "warrior", 1); profile.Settle(match.Id, "Lost", 1, true); }
            if (mode == "deck-limit") while (profile.CustomDeckCount < LocalProfile.MaximumCustomDecks) profile.CreateDraft("Test deck", "HERO_08");
            if (mode == "pending-pack" || mode == "pending-reward" || mode == "deck-limit") {
                // BeginNativePack draws a random pack into the collection, which can lower the deck's missing-card quote; the
                // native model then holds a stale price (ValidProduct false, RESTRICTED). Production re-quotes on the ledger
                // change (RefreshNeeded on shop open); the test does the same, so the check sees only the blocker.
                bool stale = products.GetProductDataModel(deckPmt).Prices[0].Amount != Quote(deck);
                UpdateAvailability(new ProductCatalog());
                check(products.GetProductDataModel(deckPmt).Availability == (stale ? ProductAvailability.RESTRICTED : ProductAvailability.LOCKED),
                    mode + " before re-quote: a stale deck quote is never offered, an unchanged one is locked");
                RefreshPrices();
                UpdateAvailability(new ProductCatalog()); int dirty = ProductCatalog.Dirty;
                // A blocked Deck stays visible (LOCKED, refused at Buy) and keeps its Recipe page variant.
                check(products.GetProductDataModel(deckPmt).Availability == ProductAvailability.LOCKED, mode + " locks ready-made deck, still shown (was " + products.GetProductDataModel(deckPmt).Availability + ")");
                UpdateAvailability(new ProductCatalog());
                check(ProductCatalog.Dirty == dirty, "unchanged availability never repopulates sections");
            }
            store.ThrowOnBlock = mode == "after-start";
            int failed = store.Failures, acks = store.Acks, gold = profile.Gold;
            long receipts = profile.PurchaseSequence;
            Buy(store, new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
            if (mode == "after-start" || mode == "save-failure" || mode == "reward-after-start" || mode == "pack-after-start") {
                check(transaction != null && auth != null, "started refusal keeps tracked transaction/auth");
                if (mode == "reward-after-start") { var match = profile.Begin("mage", "warrior", 2); profile.Settle(match.Id, "Lost", 1, true); gold = profile.Gold; }
                if (mode == "pack-after-start") { profile.Purchase("pack-standard", 1, 100, "pack-after-start"); profile.BeginNativePack(); gold = profile.Gold; receipts = profile.PurchaseSequence; }
                if (mode == "save-failure") profile.CommitFailure = _ => throw new IOException("save failed");
                TickPurchase(); profile.CommitFailure = null;
                check(store.Failures == failed + 1 && transaction != null && auth.IsShown && resultShown, "real Buy failure keeps auth until acknowledgement");
                var response = auth.Response; var tx = transaction;
                response(false, tx); response(false, tx);
                check(store.Acks == acks + 1 && transaction == null && auth == null && !store.Blocked, "failure acknowledgement clears tracked auth once");
            } else check(transaction == null && store.Failures == failed, "preflight refusal never queues native auth " + mode);
            check(store.OrphanPopups == 0 && profile.Gold == gold && profile.PurchaseSequence == receipts, "refusal leaves no orphan popup/debit " + mode);
            if (profile.PendingRewardPresentation != null) profile.AcknowledgeRewardPresentation(profile.PendingRewardPresentation.Id);
            if (profile.PackRevealPending) profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(c => c.Key));
            if (mode == "deck-limit") foreach (var draft in profile.CustomDecks) profile.DeleteDraft(draft.Id);
            if (mode == "pending-pack" || mode == "pending-reward" || mode == "deck-limit") {
                int dirty = ProductCatalog.Dirty; RefreshPrices(); UpdateAvailability(new ProductCatalog());
                // Pending pack/reward also hid the other kinds (one repopulation); a deck-limit block hid nothing.
                check(products.GetProductDataModel(deckPmt).Availability == ProductAvailability.CAN_PURCHASE
                    && ProductCatalog.Dirty == dirty + (mode == "deck-limit" ? 0 : 1), mode + " cleared: deck purchasable again");
            }
        }
        store.ThrowOnBlock = false;
        // Tray-capacity seam (live tray full beside this session's loaners) refuses ready-made decks only.
        var packOffer = NativeShopPurchase.Products.First(p => p.Kind == "Pack");
        NativeShopPurchase.PersonalDeckBlockerSeam = _ => NativeUIProjection.TrayFullMessage;
        try { check(NativeShopPurchase.Blocker(profile, deck) == NativeUIProjection.TrayFullMessage && NativeShopPurchase.Blocker(profile, packOffer) == null,
            "a full live tray refuses ready-made decks through the seam, never other kinds"); }
        finally { NativeShopPurchase.PersonalDeckBlockerSeam = null; }
        long future = DateTime.UtcNow.Ticks + 1000000;
        profile.Purchase("pack-standard", 1, 100, future.ToString(System.Globalization.CultureInfo.InvariantCulture));
        check(NativeShopPurchase.NextTransactionId(profile, 0, future - 100) == future + 1, "clock rollback cannot reuse saved request id");
        profile.Dispose();
        using var reopened = new LocalProfile(path);
        check(NativeShopPurchase.NextTransactionId(reopened, 0, future - 100) == future + 1, "clock rollback safe after restart");
    }
    private static void RunDeckCompletion(Action<bool, string> check, Func<string, int, string> fresh)
    {
        foreach (bool liveTray in new[] { false, true }) {
            string directory = Path.Combine(Path.GetDirectoryName(fresh("native-completion-root-" + liveTray, 0))!, "native-completion-" + liveTray);
            Directory.CreateDirectory(directory);
            using var profile = new LocalProfile(Path.Combine(directory, "profile.xml"));
            check(profile.GrantWelcomeIfNew(), "completion fixture has welcome kit");
            profile.CommitChange(next => { next.GoldBalance = 100000; return true; });
            var product = NativeShopPurchase.Products.Single(p => p.Id == "deck-burn-mage");
            var recipe = LocalShop.Default.Recipe(product.RecipeId);
            profile.Purchase("recipe-burn-mage", 1, profile.PurchasePrice("recipe-burn-mage"), "recipe");
            var built = LocalShop.Default.Library.BuildDeck(recipe, _ => 0).Deck;
            var draft = profile.SaveDeckFromRecipe(recipe.Id, new LocalCustomDeck { Name = built.Name, Hero = built.Hero,
                Cards = built.CardIds, Sideboards = built.Sideboards.Select(board => new LocalSideboard { Source = board.Key, Cards = board.Value.ToList() }).ToList() });
            if (liveTray) NativeShopPurchase.PersonalDeckBlockerSeam = _ => NativeUIProjection.TrayFullMessage;
            else while (profile.CustomDeckCount < LocalProfile.MaximumCustomDecks) profile.CreateDraft("Filler", "HERO_08");
            try {
                string capacity = liveTray ? NativeUIProjection.TrayFullMessage : LocalProfile.PersonalDeckLimitMessage;
                check(NativeShopPurchase.PersonalDeckBlocker(profile) == capacity && NativeShopPurchase.Blocker(profile, product) == null,
                    "full tray permits in-place completion via native blocker: live=" + liveTray);
                check(NativeShopKitChoice.KitUsable(profile, new[] { product }, capacity), "full tray kit is usable for in-place completion");
                profile.CommitChange(next => { next.CustomDecks.Single(deck => deck.Id == draft.Id).Cards.RemoveAt(0); return true; });
                check(NativeShopPurchase.Blocker(profile, product) == capacity
                    && !NativeShopKitChoice.KitUsable(profile, new[] { product }, capacity), "modified recipe needs slot and hides kit badge at capacity");
                profile.SaveDraft(draft.Id, draft.Name, draft.Hero, built.CardIds, draft.SortOrder);
                var row = ((MiniJSON.JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<MiniJSON.JsonNode>().Single(p => (string)p["id"] == product.Id);
                check((bool)row["purchasable"] && row["reason"] == null, "test-control offers in-place completion at capacity");
                int count = profile.CustomDeckCount;
                if (liveTray) {
                    Reseed(profile); RefreshPrices();
                    long pmt = NativeShopPurchase.PmtId(product);
                    var model = products.GetProductDataModel(pmt);
                    UpdateAvailability(new ProductCatalog());
                    check(model.Availability == ProductAvailability.CAN_PURCHASE, "native availability allows completion beside session loaners");
                    int price = profile.PurchasePrice(product.Id);
                    kits.Ask(pmt, price, profile.DeckKitFor(price)); kits.Answer(true, Time.realtimeSinceStartup);
                    check(kits.Dispatch(pmt, price, Quote(product), model.Prices[0].Amount, Time.realtimeSinceStartup), "completion kit order accepted");
                    Buy(StoreManager.Get(), new BuyProductEventArgs(pmt, CurrencyType.GOLD, 1, true));
                    check(transaction != null, "native completion starts transaction at full live tray");
                    TickPurchase();
                    check(committed && profile.Purchases.Last().Granted.DeckId == draft.Id, "native completion commits existing deck");
                    auth.Response(true, transaction);
                } else TestControlShop.Purchase(profile, "complete", product.Id, 1, profile.DeckKits.Single().Id, true);
                check(profile.CustomDeckCount == count && profile.Purchases.Last().Granted.DeckId == draft.Id
                    && profile.MissingDeckSlotCount(draft.Id) == 0, "capacity completion grants missing cards without allocating deck");
            } finally { NativeShopPurchase.PersonalDeckBlockerSeam = null; RecoverUI(); }
        }
    }
    // #57: a ready-made deck order with an unredeemed kit asks USE KIT / PAY FULL first; the answer re-places the order
    // (production AskDeckKit callback: USE KIT at the after-kit amount, PAY FULL at the catalogue quote).
    private static void RunKits(Action<bool, string> check, Func<string, int, string> fresh)
    {
        // Own directory: a new save beside another save's backups/ is not a new player (GrantWelcomeIfNew fails closed).
        string kitDir = Path.Combine(Path.GetDirectoryName(fresh("native-kits-root", 0))!, "native-kits"); Directory.CreateDirectory(kitDir);
        string path = Path.Combine(kitDir, "profile.xml");
        File.Delete(path);
        using var profile = new LocalProfile(path);
        check(profile.GrantWelcomeIfNew() && profile.Gold == LocalWelcomeGift.Gold, "welcome save for the kit flow");
        Reseed(profile);
        var manager = ServiceManager.Manager = new PurchaseManager();
        RefreshPrices();
        var deck = NativeShopPurchase.Products.Where(p => p.Kind == "Deck" && profile.PurchasePrice(p.Id) > 0)
            .OrderBy(p => profile.PurchasePrice(p.Id)).First();
        int price = profile.PurchasePrice(deck.Id), due = Math.Max(0, price - LocalWelcomeGift.KitGoldCap);
        check(due <= profile.Gold, "the cheapest ready-made deck is affordable with the welcome kit: " + price);
        var model = products.GetProductDataModel(NativeShopPurchase.PmtId(deck));
        // Live 2026-10-28: native greys and refuses Buy below its displayed price, so a kit deck is quoted after the kit.
        var dearest = NativeShopPurchase.Products.Where(p => p.Kind == "Deck").OrderByDescending(p => profile.PurchasePrice(p.Id)).First();
        var pack = NativeShopPurchase.Products.First(p => p.Kind == "Pack");
        int dearPrice = profile.PurchasePrice(dearest.Id);
        UpdateAvailability(new ProductCatalog());
        check(model.Prices[0].Amount == due, "after-kit native quote " + model.Prices[0].Amount + " vs " + due);
        var kitGold = products.Infos[NativeShopPurchase.PmtId(deck)].Prices[CurrencyType.GOLD];
        var packGold = products.Infos[NativeShopPurchase.PmtId(NativeShopPurchase.Products.First(p => p.Kind == "Pack"))].Prices[CurrencyType.GOLD];
        check(kitGold.CurrentPrice.RealPrice == due && kitGold.OriginalPrice.RealPrice == due && packGold.OriginalPrice.RealPrice == packGold.CurrentPrice.RealPrice
            && profile.HasUnredeemedDeckKit, "kit original and current prices both equal the after-kit quote; the kit badge condition holds");
        check(NativeShopPurchase.Products.Where(p => p.Kind == "Deck" || p.Kind == "Recipe").All(p => {
            var bundle = BuildBundle(p, NativeShopKitChoice.NativeQuote(profile, p, profile.PurchasePrice(p.Id)));
            var gold = products.Infos[NativeShopPurchase.PmtId(p)].Prices[CurrencyType.GOLD];
            return !bundle.Attributes["tags"].Split(',').Contains("on_sale")
                && bundle.GoldCost.Value.OriginalCost == bundle.GoldCost.Value.CurrentCost
                && gold.OriginalPrice.RealPrice == gold.CurrentPrice.RealPrice
                && !products.GetProductDataModel(NativeShopPurchase.PmtId(p)).Tags.Contains("on_sale");
        }), "every Deck and Recipe keeps a plain quote with a kit available");
        check(model.Prices[0].Amount == due && ValidProduct(model) && ModelPricesMatch()
            && products.GetProductDataModel(NativeShopPurchase.PmtId(dearest)).Prices[0].Amount == dearPrice - LocalWelcomeGift.KitGoldCap
            && dearPrice - LocalWelcomeGift.KitGoldCap > profile.Gold
            && products.GetProductDataModel(NativeShopPurchase.PmtId(pack)).Prices[0].Amount == profile.PurchasePrice(pack.Id)
            && model.Availability == ProductAvailability.CAN_PURCHASE,
            "kit decks are quoted natively after the kit (out-of-reach decks stay above the wallet); other kinds keep their price");
        check(NativeShopKitChoice.NativeQuote(profile, deck, price) == due && NativeShopKitChoice.NativeQuote(profile, pack, 4000) == 4000
            && NativeShopKitChoice.NativeQuote(profile, deck, 2000) == 0, "native quote rule: max(0, price - kit cap) for decks only");
        // #57 review: the kit badge means "usable now": some offered priced deck takes the kit and a personal deck fits.
        check(NativeShopKitChoice.KitUsable(profile, NativeShopPurchase.Products, null)
            && !NativeShopKitChoice.KitUsable(profile, NativeShopPurchase.Products, LocalProfile.PersonalDeckLimitMessage)
            && !NativeShopKitChoice.KitUsable(profile, NativeShopPurchase.Products, NativeUIProjection.TrayFullMessage)
            && !NativeShopKitChoice.KitUsable(profile, NativeShopPurchase.Products.Where(p => p.Kind != "Deck"), null)
            && !NativeShopKitChoice.KitUsable(null, NativeShopPurchase.Products, null),
            "kit badge only while a priced deck takes the kit and the tray has room");
        string text = DeckDetails(deck, price);
        check((text.Contains(NativeShopDecks.KitLine(price, LocalWelcomeGift.KitGoldCap))
                || text.Contains(NativeShopDecks.KitLine(price, LocalWelcomeGift.KitGoldCap, true)))
            && text.Contains("— " + due + " gold.") && text.Length <= NativeShopDecks.DetailsLimit,
            "deck page quotes the after-kit price and names the kit and the full price: " + text);
        void Order(PriceDataModel order) => manager.PurchaseProduct(model, order, 1);
        Time.realtimeSinceStartup += 1;
        int bought = manager.Bought;
        Order(model.Prices[0]);
        check(kitQuestions.Count == 1 && kitQuestions[0] == (model.PmtId, LocalWelcomeGift.KitId, price) && transaction == null
            && manager.Bought == bought && kits.Asking, "deck order with a kit asks first; no order placed");
        kitQuestion = new AlertPopup.PopupInfo();
        Order(model.Prices[0]);
        check(kitQuestions.Count == 1 && transaction == null, "clicks while the question is open are ignored");
        kitQuestion = null;
        // Escape / cancel: nothing ordered, the next click asks again.
        kits.Cancel(); Order(model.Prices[0]);
        check(kitQuestions.Count == 2 && transaction == null, "cancelled question: the next order asks again");
        // PAY FULL with a short balance: the native not-enough-gold stop, kit untouched.
        int popups = DialogManager.Get().Shown.Count;
        check(kits.Answer(false, Time.realtimeSinceStartup) == price, "PAY FULL charges the pre-kit price");
        Order(new PriceDataModel { Currency = CurrencyType.GOLD, Amount = price });
        check(transaction == null && DialogManager.Get().Shown.Count == popups + 1 && DialogManager.Get().Shown.Last().m_id == "solostone-not-enough-gold"
            && profile.DeckKits.Single().RedeemedPurchaseSequence == null, "PAY FULL short of gold: not-enough-gold, no order, kit kept");
        // An answer only covers its own order: another price or an expired window asks again.
        Order(model.Prices[0]); check(kitQuestions.Count == 3, "an answer is used once");
        check(kits.Answer(true, Time.realtimeSinceStartup) == due && !kits.Answered(model.PmtId, price + 1, Time.realtimeSinceStartup)
            && !kits.Answered(model.PmtId, price, Time.realtimeSinceStartup + NativeShopKitChoice.Window), "answer bound to product, quote and window");
        // USE KIT: the order carries the after-kit amount; the order guard, Buy and the commit use it and redeem the kit.
        int gold = profile.Gold; long receipts = profile.PurchaseSequence;
        Order(model.Prices[0]);
        check(transaction != null && auth != null && purchaseKit == LocalWelcomeGift.KitId && !kits.Answered(model.PmtId, price, Time.realtimeSinceStartup),
            "USE KIT starts one transaction carrying the kit; the answer is consumed");
        Finish();
        var receipt = profile.Purchases.Last();
        check(profile.PurchaseSequence == receipts + 1 && receipt.KitId == LocalWelcomeGift.KitId && receipt.PriceBeforeKit == price
            && receipt.GoldPaid == due && profile.Gold == gold - due
            && profile.DeckKits.Single().RedeemedPurchaseSequence == receipt.Sequence, "kit redeemed in the native commit: gold paid " + due);
        // The answer binds to the one order it re-places: a wrong amount consumes it, and an order that dies before Buy
        // leaves nothing a later unasked click could reuse (review 2026-10-28).
        var kitX = new LocalDeckKit { Id = "kit:x", GoldCap = 2500 };
        var choice = new NativeShopKitChoice();
        choice.Ask(7, 3000, kitX);
        check(choice.Answer(true, 1) == 500 && !choice.Dispatch(7, 3000, 500, 3000, 1) && !choice.Answered(7, 3000, 1) && !choice.Dispatch(7, 3000, 500, 500, 1),
            "a USE KIT answer only accepts its after-kit amount, and a refused order consumes it");
        choice.Ask(7, 3000, kitX); choice.Answer(true, 1);
        check(choice.Dispatch(7, 3000, 500, 500, 1) && !choice.Answered(7, 3000, 1) && choice.Due(7, 3000, 2) == 500, "the answer moves onto the dispatched order");
        check(!choice.Dispatch(7, 3000, 500, 500, 3) && !choice.Dispatch(7, 3000, 500, 3000, 3) && choice.Due(7, 3000, 3) == 3000 && choice.Take(7, 3000, 3) == null,
            "an order that died before Buy: a later unasked order on a kit-quoted deck is refused and uses no kit");
        check(choice.Dispatch(8, 3000, 3000, 3000, 3) && !choice.Dispatch(8, 3000, 3000, 2999, 3), "an unkitted deck orders at its price only");
        choice.Ask(7, 3000, kitX); choice.Answer(true, 1);
        check(choice.Dispatch(7, 3000, 500, 500, 1) && choice.Take(7, 3000, 2, out bool boundKit) == "kit:x" && boundKit
            && choice.Take(7, 3000, 2, out bool boundAgain) == null && !boundAgain, "Buy takes the bound kit once");
        choice.Ask(7, 3000, kitX); choice.Answer(false, 1);
        check(choice.Dispatch(7, 3000, 500, 3000, 1) && choice.Due(7, 3000, 1) == 3000 && choice.Take(7, 3000, 1, out bool boundFull) == null && boundFull,
            "PAY FULL binds a full-price order without a kit");
        // A discounted order whose binding is lost before Buy (recovery, expiry) is refused, never charged in full.
        choice.Ask(7, 3000, kitX); choice.Answer(true, 1); choice.Dispatch(7, 3000, 500, 500, 1);
        bool orphanRefused = false;
        try { choice.Take(7, 3000, 1 + NativeShopKitChoice.Window); } catch (InvalidOperationException) { orphanRefused = true; }
        check(orphanRefused && choice.Take(7, 3000, 2) == null, "an expired discounted order is refused once, then nothing lingers");
        // PAY FULL needs a release on that button in the CANCEL's frame; Escape or a press dragged off never pays in full.
        var release = new NativeShopKitRelease();
        check(!release.Cancel(10) && release.Released(10), "native callback first: the same-frame release completes PAY FULL");
        release.Reset();
        check(!release.Released(11) && release.Cancel(11), "release first: the same-frame CANCEL is PAY FULL");
        release.Reset();
        check(!release.Cancel(12) && !release.Expired(12) && release.Expired(13), "Escape: CANCEL with no release cancels after its frame");
        release.Reset();
        check(!release.Cancel(20) && !release.Released(21) && release.Expired(21), "a release in a later frame (drag-off press, then mouse-up) never pays");
        // A discounted order without an answer is refused by the order guard.
        var next = NativeShopPurchase.Products.Where(p => p.Kind == "Deck" && !NativeShopPurchase.Owned(profile, p, profile.PurchasePrice(p.Id)))
            .OrderBy(p => profile.PurchasePrice(p.Id)).First();
        RefreshPrices();
        var nextModel = products.GetProductDataModel(NativeShopPurchase.PmtId(next));
        var nextGold = products.Infos[NativeShopPurchase.PmtId(next)].Prices[CurrencyType.GOLD];
        check(!profile.HasUnredeemedDeckKit && nextGold.OriginalPrice.RealPrice == nextGold.CurrentPrice.RealPrice,
            "the last kit used: badges off and original still equals current price");
        check(NativeShopPurchase.Products.Where(p => p.Kind == "Deck" || p.Kind == "Recipe").All(p => !products.GetProductDataModel(NativeShopPurchase.PmtId(p)).Tags.Contains("on_sale")),
            "Deck and Recipe prices remain plain after redeeming the kit");
        check(nextModel.Prices[0].Amount == profile.PurchasePrice(next.Id) && !DeckDetails(next, profile.PurchasePrice(next.Id)).Contains("Deck kit"),
            "the redeemed kit leaves the quotes: every deck back at its full price");
        int cancelled = manager.Cancelled, asked = kitQuestions.Count;
        Order(model.Prices[0]); manager.PurchaseProduct(nextModel, nextModel.Prices[0], 1);
        check(transaction == null && kitQuestions.Count == asked, "no kit left: no question");
        var forged = new PurchaseManager.PurchaseOrder { Product = nextModel, Price = new PriceDataModel { Currency = CurrencyType.GOLD, Amount = 0 } };
        System.Collections.IEnumerator routine = null;
        check(!GuardPurchase(manager, forged, ref routine) && forged.HasFinished && manager.Cancelled == cancelled + 1 && transaction == null,
            "an after-kit amount without an answered kit question is refused by the order guard");
        check(NativeShopDecks.KitLine(price, 0) == "" && !DeckDetails(next, profile.PurchasePrice(next.Id)).Contains("deck kit"), "no kit, no kit line");
    }
    // Owner 2026-10-25: pack tile = one buy click; other kinds open native details, then one buy click; never a confirmation.
    private static void Reseed(LocalProfile profile)
    {
        Profile = profile; products = new Service(); RecoverUI();
        foreach (var p in NativeShopPurchase.Products) products.SetProduct(BuildBundle(p, profile.PurchasePrice(p.Id)));
    }
    private static ShopSlot Tile(LocalShopProduct product, ProductDataModel shown = null) =>
        new ShopSlot { BrowserButtonDataModel = new BrowserButton { DisplayProduct = shown ?? products.GetProductDataModel(NativeShopPurchase.PmtId(product)) } };
    // Release frame of a click: RELEASE listeners (BuyPackTile), then RELEASEALL (ClearTilePress).
    private static bool Release(ShopSlot slot)
    {
        Time.frameCount++;
        bool result = BuyPackTile(slot);
        ClearTilePress(slot.Clickable);
        return result;
    }
    // Press on one frame (Clickable.OnClick), release on a later one at the same cursor position.
    private static bool Tap(ShopSlot slot) { NoteTilePress(slot.Clickable); return Release(slot); }
    private static (bool details, PurchaseManager manager) Click(LocalShopProduct product)
    {
        var model = products.GetProductDataModel(NativeShopPurchase.PmtId(product));
        bool details = Tap(Tile(product));
        if (details) ServiceManager.Manager.PurchaseProduct(model, model.Prices[0], 1); // details page price button (ProductPage.TryBuy)
        return (details, ServiceManager.Manager);
    }
    // Commit, then the native success window acknowledgement.
    private static void Finish()
    {
        TickPurchase(); TickPurchase();
        var tx = transaction; auth.Response(true, tx);
    }
    private static void RunClicks(Action<bool, string> check, Func<string, int, string> fresh)
    {
        var store = StoreManager.Get(); var dialogs = DialogManager.Get(); int failuresBefore = errors.Count;
        using (var profile = new LocalProfile(fresh("native-clicks", 100000)))
        {
            Reseed(profile);
            var manager = ServiceManager.Manager = new PurchaseManager();
            long previousId = 0;
            RefreshPrices();
            var firstDeck = NativeShopPurchase.Products.First(p => p.Kind == "Deck" && !NativeShopPurchase.Owned(profile, p, profile.PurchasePrice(p.Id)));
            string DeckText() => DeckDetails(firstDeck, profile.PurchasePrice(firstDeck.Id));
            string deckBefore = DeckText();
            check(ReferenceEquals(DeckText(), deckBefore), "deck text is reused while the ledger, shop and quote are unchanged");
            foreach (string kind in new[] { "Pack", "Recipe", "Deck", "CardBack", "HeroSkin", "HubTheme", "Board", "Pet", "Pack" })
            {
                Time.realtimeSinceStartup += 1; // Outside the pack cooldown of the previous acknowledgement.
                var product = NativeShopPurchase.Products.First(p => p.Kind == kind && !NativeShopPurchase.Owned(profile, p, profile.PurchasePrice(p.Id)));
                int gold = profile.Gold, price = profile.PurchasePrice(product.Id), popups = dialogs.Shown.Count;
                long receipts = profile.PurchaseSequence; int started = manager.Started, bought = manager.Bought;
                var (details, _) = Click(product);
                check(details == (kind != "Pack" && kind != "HubTheme" && kind != "Board" && kind != "Pet"), kind + ": pack, theme and board tiles buy at once; other kinds open the native details page");
                check(manager.Confirmations == 0 && manager.Bought == bought + 1 && transaction != null && auth != null, kind + ": one click starts one transaction, no confirmation");
                check(manager.Current == null, kind + ": finished purchase routine leaves no stale native handle");
                var tx = transaction;
                check(tx.ID > previousId && profile.Gold == gold, kind + ": monotonic durable request id, no debit before commit");
                previousId = tx.ID;
                Click(product); Click(product); // rapid double click and re-entrant click while pending
                check(transaction == tx && manager.Started == started + 1 && manager.Bought == bought + 1, kind + ": clicks while pending start nothing");
                TickPurchase(); TickPurchase();
                Click(product);
                check(profile.PurchaseSequence == receipts + 1 && profile.Gold == gold - price && transaction == tx,
                    kind + ": exactly one commit and debit per click");
                var response = auth.Response; response(true, tx); response(true, tx);
                check(transaction == null && auth == null && !store.Blocked && profile.PurchaseSequence == receipts + 1
                    && dialogs.Shown.Count == popups, kind + ": acknowledged once; no extra popup");
            }
            check(manager.Confirmations == 0 && errors.Count == failuresBefore, "no native confirmation or shop failure in the click flow");
            // Owner 2026-10-25: buying equips; clicking an owned theme or board tile equips it, with no order.
            var boughtTheme = NativeShopPurchase.Products.First(p => p.Kind == "HubTheme" && p.Price > 0 && profile.OwnsHubTheme(p.HubTheme));
            var boughtBoard = NativeShopPurchase.Products.First(p => p.Kind == "Board" && p.Price > 0 && profile.OwnsBoard(p.Board.Value));
            check(profile.SelectedHubTheme == boughtTheme.HubTheme && profile.SelectedBoard == boughtBoard.Board
                && themeNoticeAfterPurchase == boughtTheme.HubTheme, "bought theme and board are equipped; theme notice queued for after the purchase window");
            check(products.GetProductDataModel(NativeShopPurchase.PmtId(boughtTheme)).Name.EndsWith("\nEquipped")
                && products.GetProductDataModel(NativeShopPurchase.PmtId(NativeShopPurchase.Products.Single(p => p.Id == "theme-CLASSIC"))).Name == "Classic"
                && !products.GetProductDataModel(NativeShopPurchase.PmtId(NativeShopPurchase.Products.First(p => p.Kind == "HubTheme" && !profile.OwnsHubTheme(p.HubTheme)))).Name.Contains("Equip"),
                "tile names: Equipped line only on the equipped tile; owned and unowned show the plain name");
            foreach (var target in new[] { "theme-random", "theme-CLASSIC", "board-random", "board-3" }.Select(id => NativeShopPurchase.Products.Single(p => p.Id == id))) {
                Time.realtimeSinceStartup += 1;
                int gold = profile.Gold, orders = manager.Calls; long receipts = profile.PurchaseSequence;
                var (details, _) = Click(target);
                check(!details && manager.Calls == orders && profile.Gold == gold && profile.PurchaseSequence == receipts && transaction == null
                    && (target.Kind == "HubTheme" ? profile.SelectedHubTheme == target.HubTheme : profile.SelectedBoard == target.Board),
                    target.Id + ": owned tile equips without an order or debit");
                check(products.GetProductDataModel(NativeShopPurchase.PmtId(target)).Name.EndsWith("\nEquipped")
                    && !products.GetProductDataModel(NativeShopPurchase.PmtId(target.Kind == "HubTheme" ? boughtTheme : boughtBoard)).Name.Contains("Equip"),
                    target.Id + ": tile names follow the equipped look");
            }
            check(themeNotices.Count(key => key == LocalHubCosmetics.ClassicTheme) == 1, "theme equip asks for the next-start notice");
            check(DeckText() != deckBefore, "deck description recomputed from the ledger after a purchase");
            string cached = DeckText();
            check(ReferenceEquals(DeckText(), cached), "deck text cached again for the new ledger");
            var shopBefore = LocalShop.Default; var overrideBefore = LocalShop.TestOverride;
            try
            {
                using var catalogSource = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
                using var verified = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new StreamReader(catalogSource!).ReadToEnd().Replace("\"verify\": true", "\"verify\": false")));
                var swapped = LocalShop.TestOverride = LocalShop.Load(verified, () => shopBefore.Library);
                string afterSwap = DeckText();
                check(ReferenceEquals(deckDetailsShop, swapped) && !ReferenceEquals(deckDetailsShop, shopBefore), "deck text cache is keyed to the swapped shop");
                check(ReferenceEquals(DeckText(), afterSwap), "deck text cached for the swapped shop");
            }
            finally { LocalShop.TestOverride = overrideBefore; }
            DeckText();
            check(ReferenceEquals(deckDetailsShop, shopBefore), "swapping the shop back drops the swapped shop's cache");
            var pack = NativeShopPurchase.Products.First(p => p.Kind == "Pack");
            // Refusals stay before any transaction: owned, stale quote, pending pack reveal and pending reward.
            var recipe = NativeShopPurchase.Products.First(p => p.Kind == "Recipe" && NativeShopPurchase.Owned(profile, p, profile.PurchasePrice(p.Id)));
            var deck = NativeShopPurchase.Products.First(p => p.Kind == "Deck" && !NativeShopPurchase.Owned(profile, p, profile.PurchasePrice(p.Id)));
            foreach (string mode in new[] { "owned", "stale-pack", "stale-deck", "pending-reveal", "pending-reward" })
            {
                Time.realtimeSinceStartup += 1;
                RefreshPrices();
                var target = mode == "owned" ? recipe : mode == "stale-deck" || mode == "pending-reward" ? deck : pack;
                if (mode.StartsWith("stale")) products.GetProductDataModel(NativeShopPurchase.PmtId(target)).Prices[0].Amount++;
                if (mode == "pending-reveal") profile.BeginNativePack();
                if (mode == "pending-reward") profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(c => c.Key));
                if (mode == "pending-reward") { var match = profile.Begin("mage", "warrior", 1); profile.Settle(match.Id, "Lost", 1, true); }
                int gold = profile.Gold, failures = store.Failures, bought = manager.Bought; long receipts = profile.PurchaseSequence;
                Click(target); TickPurchase();
                check(transaction == null && auth == null && store.Failures == failures && profile.Gold == gold
                    && profile.PurchaseSequence == receipts && manager.Confirmations == 0 && manager.Current == null,
                    mode + ": refused before a transaction, no debit, native order released");
                check(mode == "owned" || mode.StartsWith("pending") ? manager.Bought == bought + 1 : manager.Bought == bought,
                    mode + ": refusal point (Buy preflight or order guard)");
            }
            profile.AcknowledgeRewardPresentation(profile.PendingRewardPresentation.Id);
            int quoteVersion = NativeShopPurchase.CatalogueVersion;
            RefreshPrices();
            check(NativeShopPurchase.CatalogueVersion > quoteVersion, "RefreshPrices bumps the catalogue version");
            // Shop open skips the rebuild only while nothing it depends on changed since a rebuild that completed.
            bool Fresh() => priceRefresh.IsFresh(Profile, Profile.LedgerIdentity, LocalShop.Default, products, NativeShopPurchase.CatalogueVersion);
            check(Fresh(), "a complete rebuild marks the native models fresh");
            NativeShopPurchase.CatalogueVersion++; check(!Fresh(), "a later catalogue bump (seed, reseed, rebuild) forces the next rebuild");
            RefreshPrices(); check(Fresh(), "rebuilt");
            var service = products; products = null;
            try { RefreshPrices(); } catch (NullReferenceException) { }
            products = service;
            check(!Fresh(), "a rebuild that throws midway never marks itself fresh");
            RefreshPrices(); check(Fresh(), "a later complete rebuild is fresh again");
            check(!RefreshNeeded(), "fresh inputs and matching native quotes skip the rebuild on open");
            var quoted = NativeShopPurchase.Products.First(p => p.Kind == "Pack");
            var quotedModel = products.GetProductDataModel(NativeShopPurchase.PmtId(quoted));
            quotedModel.Prices[0].Amount++;
            check(Fresh() && RefreshNeeded(), "fresh key but a native price differing from the memoized quote still rebuilds on open");
            quotedModel.Prices[0].Amount--; check(!RefreshNeeded(), "restored quote skips again");
            var savedPrices = quotedModel.Prices; quotedModel.Prices = new();
            check(RefreshNeeded(), "a model without its price entry rebuilds on open");
            quotedModel.Prices = savedPrices;
            var savedModels = products.Models.ToDictionary(m => m.Key, m => m.Value); products.Models.Remove(NativeShopPurchase.PmtId(quoted));
            check(RefreshNeeded(), "a missing native model rebuilds on open");
            foreach (var model in savedModels) products.Models[model.Key] = model.Value;
            check(!RefreshNeeded(), "models restored, skipped again");
            Profile.SelectCardBack(0); check(!Fresh() && RefreshNeeded(), "any commit (new ledger) forces the next rebuild");
            RefreshPrices();
            var state = new NativeShopRefreshState();
            object p1 = new(), l1 = new(), s1 = new(), v1 = new();
            check(!state.IsFresh(p1, l1, s1, v1, 3), "a new state is never fresh");
            state.Complete(p1, l1, s1, v1, 3);
            check(state.IsFresh(p1, l1, s1, v1, 3) && !state.IsFresh(new object(), l1, s1, v1, 3) && !state.IsFresh(p1, new object(), s1, v1, 3)
                && !state.IsFresh(p1, l1, new object(), v1, 3) && !state.IsFresh(p1, l1, s1, new object(), 3) && !state.IsFresh(p1, l1, s1, v1, 4),
                "freshness needs the same profile, ledger, shop, service and catalogue version");
            state.Invalidate(); check(!state.IsFresh(p1, l1, s1, v1, 3), "invalidated until the next complete rebuild");

            // Drag-scroll release on a pack tile: no order, no details page, whichever release listener runs first.
            Time.realtimeSinceStartup += 1; Time.frameCount++;
            int calls = manager.Calls;
            var dragged = Tile(pack); NoteTilePress(dragged.Clickable); dragged.Clickable.m_dragged = true;
            check(!Release(dragged) && manager.Calls == calls && transaction == null, "drag release (tile listener first): no purchase or details page");
            dragged = Tile(pack); NoteDragRelease(dragged.Clickable); check(dragReleaseClickable == null, "plain release records no drag");
            NoteTilePress(dragged.Clickable); Time.frameCount++;
            dragged.Clickable.m_dragged = true; NoteDragRelease(dragged.Clickable); dragged.Clickable.m_dragged = false; // native reset
            check(!BuyPackTile(dragged) && manager.Calls == calls && transaction == null, "drag release (Clickable listener first): no purchase");
            ClearTilePress(dragged.Clickable);
            // The drag decision uses this tile's own press position, never PegUI's last mouse-down.
            var moved = Tile(pack); InputCollection.Mouse = new Point { x = 400, y = 300 }; NoteTilePress(moved.Clickable);
            InputCollection.Mouse = new Point { x = 400, y = 311 }; // 11 px > 5 px tolerance x dpi 2
            check(!Release(moved) && manager.Calls == calls && transaction == null, "release beyond the tile's drag tolerance from its press: no purchase");
            InputCollection.Mouse = new Point { x = 400, y = 300 };
            check(!Release(moved) && manager.Calls == calls, "press consumed by its release: a second release is not a click");
            check(!Release(Tile(pack)) && manager.Calls == calls && transaction == null, "release with no press on this tile: no purchase");
            var tileA = Tile(pack); var tileB = Tile(pack); NoteTilePress(tileA.Clickable);
            check(!Release(tileB) && manager.Calls == calls && transaction == null, "press on tile A, release on tile B: no purchase");
            ClearTilePress(tileB.Clickable); check(pressClickable == tileA.Clickable, "release-all of another tile keeps tile A's press");
            ClearTilePress(tileA.Clickable); check(pressClickable == null, "tile A release-all clears its press");
            var axis = Tile(pack); axis.Clickable.Peg.Tolerance = new Point { x = 0, y = 0 }; NoteTilePress(axis.Clickable);
            InputCollection.Mouse = new Point { x = 900, y = 700 };
            check(NativeShopClicks.BeyondDragTolerance(10, 0, 5, 0, 1) && !NativeShopClicks.BeyondDragTolerance(10, 10, 0, 0, 1)
                && !NativeShopClicks.BeyondDragTolerance(9, -10, 5, 5, 2) && NativeShopClicks.BeyondDragTolerance(0, -11, 5, 5, 2),
                "PegUI tolerance rule: per axis, dpi-scaled, zero axis never drags");
            ClearTilePress(axis.Clickable);
            InputCollection.Mouse = new Point { x = 123, y = 456 };
            Time.frameCount++; // The recorded drag belongs to an earlier frame's release.
            long before = profile.PurchaseSequence;
            Tap(dragged); // Semantic press: TriggerPress on one frame, release at the same cursor on a later frame.
            check(transaction != null && manager.Calls == calls + 1 && pressClickable == null, "semantic press and release at one position buys");
            Finish();
            check(profile.PurchaseSequence == before + 1, "drag guard leaves exactly one purchase");
            InputCollection.Mouse = new Point { x = 123, y = 459 }; // within tolerance
            Time.realtimeSinceStartup += 1; Time.frameCount++; before = profile.PurchaseSequence;
            var jitter = Tile(pack); NoteTilePress(jitter.Clickable); InputCollection.Mouse = new Point { x = 126, y = 450 };
            Release(jitter);
            check(transaction != null && manager.Calls == calls + 2, "release within tolerance of the press buys");
            Finish();
            check(profile.PurchaseSequence == before + 1, "exactly one pack per click");

            // The tile must show the charged quote.
            Time.realtimeSinceStartup += 1; calls = manager.Calls;
            var catalogue = products.GetProductDataModel(NativeShopPurchase.PmtId(pack));
            var stale = new ProductDataModel { PmtId = catalogue.PmtId, Items = catalogue.Items,
                Prices = new() { new PriceDataModel { Currency = CurrencyType.GOLD, Amount = catalogue.Prices[0].Amount + 1 } } };
            check(!Tap(Tile(pack, stale)) && manager.Calls == calls && transaction == null, "tile price differing from catalogue refused before an order");
            var same = new ProductDataModel { PmtId = catalogue.PmtId, Items = catalogue.Items,
                Prices = new() { new PriceDataModel { Currency = CurrencyType.GOLD, Amount = catalogue.Prices[0].Amount } } };
            Tap(Tile(pack, same));
            check(transaction != null && manager.Calls == calls + 1, "tile copy with the catalogue quote buys");
            Finish();

            // An exception on the click path while a purchase is pending drops the click; the purchase completes.
            Time.realtimeSinceStartup += 1;
            Click(pack);
            var pendingTx = transaction; var pendingAuth = auth; int errorsBefore = errors.Count, acks = store.Acks, failuresNow = store.Failures;
            long pendingReceipts = profile.PurchaseSequence; int pendingGold = profile.Gold, packPrice = profile.PurchasePrice(pack.Id);
            check(!Tap(new ShopSlot { BrowserButtonDataModel = Tile(pack).BrowserButtonDataModel, Broken = true })
                && transaction == pendingTx && auth == pendingAuth && errors.Count == errorsBefore
                && refusals.Last().StartsWith("pack click while a purchase is pending"), "exception while pending: logged only, purchase kept");
            Finish();
            check(profile.PurchaseSequence == pendingReceipts + 1 && profile.Gold == pendingGold - packPrice && store.Acks == acks
                && store.Failures == failuresNow && transaction == null, "pending purchase completes; no failure acknowledgement");
            Time.realtimeSinceStartup += 1;
            Tap(new ShopSlot { BrowserButtonDataModel = Tile(pack).BrowserButtonDataModel, Broken = true });
            check(errors.Count == errorsBefore + 1 && transaction == null, "same exception with nothing pending fails closed");

            // Owner 2026-10-25: pack clicks within 0.5 s of the success window opening or its acknowledgement do nothing.
            Time.realtimeSinceStartup += 1;
            Click(pack); var cooled = transaction; TickPurchase(); TickPurchase();
            float opened = Time.realtimeSinceStartup; SuccessShown(cooled, true);
            Time.realtimeSinceStartup = opened + 0.2f; calls = manager.Calls;
            Tap(Tile(pack));
            check(manager.Calls == calls && transaction == cooled, "+0.2 s after the success window opens: ignored before any order");
            Time.realtimeSinceStartup = opened + 0.8f;
            Tap(Tile(pack));
            check(manager.Calls == calls && transaction == cooled, "tile click while the success window is still open: no order queued");
            Time.realtimeSinceStartup = opened + 0.9f; float acked = Time.realtimeSinceStartup;
            auth.Response(true, cooled);
            int coolGold = profile.Gold, coolPopups = dialogs.Shown.Count; long coolReceipts = profile.PurchaseSequence;
            Time.realtimeSinceStartup = acked + 0.2f;
            Tap(Tile(pack));
            check(manager.Calls == calls && transaction == null && profile.Gold == coolGold && profile.PurchaseSequence == coolReceipts
                && dialogs.Shown.Count == coolPopups, "+0.2 s after acknowledgement: no order, transaction, debit or popup");
            Time.realtimeSinceStartup = acked + 0.6f;
            Tap(Tile(pack));
            check(manager.Calls == calls + 1 && transaction != null, "+0.6 s after acknowledgement: the click buys");
            Finish();
            check(profile.PurchaseSequence == coolReceipts + 1 && profile.Gold == coolGold - packPrice, "exactly one more pack after the cooldown");
            check(NativeShopClicks.PackCooldownSeconds == 0.5f, "pack click cooldown is the owner's 0.5 s");
        }
        using (var poor = new LocalProfile(fresh("native-clicks-poor", 50)))
        {
            Reseed(poor);
            var manager = ServiceManager.Manager = new PurchaseManager();
            foreach (string kind in new[] { "Pack", "Deck", "CardBack" })
            {
                Time.realtimeSinceStartup += 1;
                var product = NativeShopPurchase.Products.First(p => p.Kind == kind && poor.PurchasePrice(p.Id) > 50);
                int popups = dialogs.Shown.Count;
                Click(product);
                check(transaction == null && manager.Started == 0 && poor.Gold == 50 && poor.PurchaseSequence == 0
                    && dialogs.Shown.Count == popups + 1 && dialogs.Shown.Last().m_text.StartsWith("GLUE_STORE_FAIL_NOT_ENOUGH_GOLD")
                    && dialogs.Shown.Last().m_responseDisplay == AlertPopup.ResponseDisplay.OK, kind + ": not enough gold shows the native refusal, no order or debit");
            }
            // Every route: shop not ready, or quantity above what the balance covers.
            ready = false;
            var unready = NativeShopPurchase.Products.First(p => p.Kind == "Deck" && poor.PurchasePrice(p.Id) > 50);
            int before = dialogs.Shown.Count; Click(unready);
            check(transaction == null && manager.Started == 0 && dialogs.Shown.Count == before + 1, "not enough gold shown while the shop is not ready");
            ready = true;
        }
        using (var exact = new LocalProfile(fresh("native-clicks-quantity", 100)))
        {
            Reseed(exact);
            var manager = ServiceManager.Manager = new PurchaseManager();
            var pack = NativeShopPurchase.Products.First(p => p.Kind == "Pack" && exact.PurchasePrice(p.Id) == 100);
            var model = products.GetProductDataModel(NativeShopPurchase.PmtId(pack));
            int popups = dialogs.Shown.Count;
            manager.PurchaseProduct(model, model.Prices[0], 2);
            check(transaction == null && manager.Started == 0 && exact.Gold == 100 && dialogs.Shown.Count == popups + 1
                && dialogs.Shown.Last().m_text.EndsWith("100 / 200 gold"), "quantity 2 over balance: native not-enough-gold, no order");
        }
        Isolated = false;
        check(BuyPackTile(new ShopSlot()) && LocalPurchaseOutside(), "retail paths untouched outside isolation");
        NoteDragRelease(new Clickable { m_dragged = true }); check(dragReleaseClickable is not Clickable { m_dragged: true }, "drag hook inert outside isolation");
        var retail = new Clickable(); NoteTilePress(retail); check(!ReferenceEquals(pressClickable, retail), "press hook inert outside isolation");
        Isolated = true;
        ServiceManager.Manager = null;
    }
    // Production NativeShopControlCache: one identity computation per frame/shop/page/variant/catalogue version.
    private static void RunControlCache(Action<bool, string> check)
    {
        var cache = new NativeShopControlCache();
        object shop = new(), page = new(), variant = new();
        int catalogueCalls = 0, variantCalls = 0, failures = 0;
        bool Resolve(int frame, int version, out bool local) => cache.Resolve(frame, shop, page, variant, version,
            () => { catalogueCalls++; return true; }, () => { variantCalls++; return true; }, _ => failures++, out local);
        int version = NativeShopPurchase.CatalogueVersion;
        bool all = true;
        for (int i = 0; i < 300; i++) all &= Resolve(7, version, out bool local) && local;
        check(all && catalogueCalls == 1 && variantCalls == 1, "300 elements in one frame: one catalogue check");
        NativeShopPurchase.Seed(null, null);
        check(NativeShopPurchase.CatalogueVersion > version, "Seed bumps the catalogue version");
        Resolve(7, NativeShopPurchase.CatalogueVersion, out _);
        check(catalogueCalls == 2, "catalogue version bump invalidates the same-frame cache");
        Resolve(8, NativeShopPurchase.CatalogueVersion, out _); check(catalogueCalls == 3, "next frame recomputes");
        variant = new(); Resolve(8, NativeShopPurchase.CatalogueVersion, out _); check(catalogueCalls == 4, "variant change recomputes");
        int throwing = 0; bool open = false;
        for (int i = 0; i < 300; i++)
            open |= cache.Resolve(9, shop, page, variant, NativeShopPurchase.CatalogueVersion,
                () => { throwing++; throw new InvalidOperationException("broken binding"); }, () => true, _ => failures++, out bool local) || local;
        check(!open && throwing == 1 && failures == 1, "failing identity check: fails closed, computed and reported once per frame");
        check(cache.Resolve(10, shop, page, variant, NativeShopPurchase.CatalogueVersion, () => true, () => false, _ => failures++, out bool variantLocal)
            && !variantLocal && failures == 1, "next frame retries after a failure");
        bool nullVariant = cache.Resolve(11, shop, page, null, NativeShopPurchase.CatalogueVersion, () => true,
            () => throw new InvalidOperationException("no variant"), _ => failures++, out bool noLocal);
        check(nullVariant && !noLocal && failures == 1, "no selected variant: catalogue only");
        // Test-control reveal of off-screen local tiles.
        check(NativeShopReveal.FullyVisible(true, 100, 100, 200, 300) && !NativeShopReveal.FullyVisible(true, 100, 500, 200, 300)
            && !NativeShopReveal.FullyVisible(false, 100, 100, 200, 300) && !NativeShopReveal.FullyVisible(true, 0, -134146, 200, 6000),
            "fully visible only inside the 1280x720 screen");
        check(NativeShopReveal.ShouldReveal("shop.product.1940000003", false, 0, -134146, 300, 6000, 3, 0, 5, -1), "unlaid-out local tile is revealed");
        check(!NativeShopReveal.ShouldReveal("shop.product.1940000003", true, 100, 100, 200, 300, 3, 0, 5, -1), "fully visible tile is never scrolled, hit or covered");
        check(NativeShopReveal.ShouldReveal("shop.product.1940000003", true, 100, 600, 200, 300, 3, 0, 5, -1), "partly clipped tile is revealed");
        check(!NativeShopReveal.ShouldReveal("shop.tab.decks_1", false, 0, 900, 10, 10, 3, 0, 5, -1)
            && !NativeShopReveal.ShouldReveal("home.store", false, 0, 900, 10, 10, 3, 0, 5, -1)
            && !NativeShopReveal.ShouldReveal(null, false, 0, 900, 10, 10, 3, 0, 5, -1), "only shop products are ever revealed");
        check(!NativeShopReveal.ShouldReveal("shop.product.1", false, 0, 900, 10, 10, 3, 1, 10, 5)
            && NativeShopReveal.ShouldReveal("shop.product.1", false, 0, 900, 10, 10, 3, 1, 25, 5)
            && !NativeShopReveal.ShouldReveal("shop.product.1", false, 0, 900, 10, 10, 3, NativeShopReveal.MaxAttempts, 500, 5),
            "reveal retries settle 20 frames apart and stop after 3 attempts");
        // Live 2026-10-25: a retry during layout or the scroll tween would overshoot again; it waits for a still tile.
        check(!NativeShopReveal.ShouldReveal("shop.product.1", false, 0, -395, 300, 200, 1, 1, 25, 5)
            && !NativeShopReveal.ShouldReveal("shop.product.1", false, 0, -395, 300, 200, NativeShopReveal.StableFrames - 1, 1, 500, 5)
            && NativeShopReveal.ShouldReveal("shop.product.1", false, 0, -395, 300, 200, NativeShopReveal.StableFrames, 1, 25, 5),
            "retry only once the tile's rect has stopped moving");
        check(NativeShopReveal.ShouldReveal("shop.product.1", false, 0, -134146, 300, 6000, 0, 0, 5, -1),
            "first reveal does not wait for rect stability (native layout readiness gates it)");
        check(NativeShopReveal.LaidOut(true, false) && !NativeShopReveal.LaidOut(false, false) && !NativeShopReveal.LaidOut(true, true)
            && !NativeShopReveal.LaidOut(false, true), "centre only on a ready, clean browser (pinned MoveToProductCoroutine)");
        // Live 2026-10-25 (lane 1): the browser stayed not ready or dirty at idle; the wait for it is bounded.
        int bound = NativeShopReveal.LayoutWaitFrames, still = NativeShopReveal.StableFrames;
        check(!NativeShopReveal.LayoutWaitOver(-1, 1000, still, 300, 200), "no pending frame yet: no forced reveal");
        check(!NativeShopReveal.LayoutWaitOver(100, 100 + bound - 1, still, 300, 200)
            && NativeShopReveal.LayoutWaitOver(100, 100 + bound, still, 300, 200), "layout wait ends after LayoutWaitFrames pending frames");
        check(!NativeShopReveal.LayoutWaitOver(100, 100 + 10 * bound, still - 1, 300, 200)
            && !NativeShopReveal.LayoutWaitOver(100, 100 + 10 * bound, still, 0, 200)
            && !NativeShopReveal.LayoutWaitOver(100, 100 + 10 * bound, still, 300, 0), "after the wait only a still tile with a real rect is centred");
    }
    private static bool LocalPurchaseOutside()
    {
        var options = new PurchaseManager.PurchaseManagerOptions();
        return LocalPurchase(null, null, ref options, 1) && !options.SuppressVcConfirmation;
    }
    // Owner 2026-10-25: the deck page description says what is owned and missing (the native card list cannot).
    internal static void RunDeckDetails(Action<bool, string> check, Func<string, int, string> fresh)
    {
        var library = LocalShop.Default.Library;
        var nothingOwned = RecipeLibrary.OwnedCount(new Dictionary<string, int>()); // Free and offline cards stay owned, as in the ledger.
        foreach (var product in NativeShopPurchase.Products.Where(p => p.Kind == "Deck" || p.Kind == "Recipe"))
        {
            var recipe = LocalShop.Default.Recipe(product.RecipeId);
            var none = library.MissingCopies(recipe, nothingOwned);
            var all = library.MissingCopies(recipe, id => 2);
            var someOwned = new HashSet<string>(recipe.Cards.Take(recipe.Cards.Count / 2).Select(c => c.Id));
            var some = library.MissingCopies(recipe, id => someOwned.Contains(id) ? 2 : nothingOwned(id));
            foreach (var (missing, label) in new[] { (none, "nothing owned"), (some, "some missing"), (all, "nothing missing") })
            {
                string text = NativeShopDecks.Details(product, recipe, missing, 1234);
                // The native header names the variant; the panel names the deck and label.
                string head = recipe.Name + " — " + RecipeLibrary.Label(recipe) + "\n";
                check(text.StartsWith(head), product.Id + " " + label + ": deck name and label first");
                text = text.Substring(head.Length);
                if (product.Kind == "Deck" && missing.Cards.Any(row => row.Missing > 0 && LocalWelcomeGift.HistoricalCardIds.Contains(row.Id))) {
                    check(text == LocalShop.GrantOnlyDeckMessage, product.Id + ": refusal replaces the unavailable deck price");
                    continue;
                }
                foreach (int quote in new[] { 1234, 99999 })
                    check(NativeShopDecks.Details(product, recipe, missing, quote, LocalWelcomeGift.KitGoldCap).Length <= NativeShopDecks.DetailsLimit,
                        product.Id + " " + label + ": the kit line fits the description box at quote " + quote);
                string owned = "You own " + (missing.Needed - missing.Total) + " of " + missing.Needed + " cards.";
                var groups = string.Join(", ", new[] { SabberStoneCore.Enums.Rarity.FREE, SabberStoneCore.Enums.Rarity.COMMON, SabberStoneCore.Enums.Rarity.RARE,
                    SabberStoneCore.Enums.Rarity.EPIC, SabberStoneCore.Enums.Rarity.LEGENDARY }
                    .Where(r => missing.Purchasable[r] > 0).Select(r => r.ToString().Substring(0, 1) + r.ToString().Substring(1).ToLowerInvariant() + " " + missing.Purchasable[r]));
                int boughtCore = missing.LockedCore.Sum(LocalShop.DeckGrant), lockedCore = missing.LockedCore.Sum(c => c.LockedCore) - boughtCore;
                if (boughtCore > 0) groups += (groups.Length == 0 ? "" : ", ") + "Core " + boughtCore;
                if (lockedCore > 0) groups += (groups.Length == 0 ? "" : ", ") + "locked Core " + lockedCore;
                string expectedSummary = product.Kind == "Recipe" ? "Recipe only — cards not included. " + owned
                    : missing.Total == 0 ? "You own all " + missing.Needed + " cards. Nothing missing — 1234 gold."
                    : owned + " Missing: " + missing.Total + " (" + groups + ") — 1234 gold.";
                string preview = "\n" + recipe.Preview.Trim();
                bool fitsPreview = head.Length + expectedSummary.Length + preview.Length <= NativeShopDecks.DetailsLimit;
                check(head.Length + text.Length <= NativeShopDecks.DetailsLimit && text.StartsWith(expectedSummary)
                    && text.Contains(preview) == fitsPreview,
                    product.Id + " " + label + ": summary first; preview kept only when it fits the description box");
                check(text.Split('\n')[0].Length <= 120, product.Id + " " + label + ": summary line fits two panel lines");
                if (product.Kind == "Recipe" || missing.Total == 0)
                    check(text == expectedSummary + (fitsPreview ? preview : ""), product.Id + " " + label + ": exact description");
                else
                {
                    int core = missing.LockedCore.Sum(LocalShop.DeckGrant), locked = missing.LockedCore.Sum(c => c.LockedCore) - core;
                    string summary = text.Split('\n')[0];
                    check((core == 0 || summary.Contains((missing.Purchasable.All(row => row.Value == 0) ? "(" : ", ") + "Core " + core + (locked > 0 ? ", " : ")")))
                        && summary.Contains("locked Core") == (locked > 0) && (locked == 0 || summary.Contains("locked Core " + locked + ")")),
                        product.Id + " " + label + ": bought Core copies listed as part of the price; only unscheduled Core stays locked");
                    var listed = text.Contains("\nMissing cards: ") ? text.Substring(text.IndexOf("\nMissing cards: ") + 16).Split("; ") : Array.Empty<string>();
                    var names = missing.Cards.Select(c => SabberStoneCore.Model.Cards.FromId(c.Id).Name).ToHashSet();
                    check(listed.All(n => n.StartsWith("+") && n.EndsWith(" more") || names.Contains(n.EndsWith(" x2") ? n[..^3] : n))
                        && (listed.Length == 0 || !listed.Last().StartsWith("+") || int.Parse(listed.Last()[1..^5]) == missing.Cards.Count - (listed.Length - 1)),
                        product.Id + " " + label + ": only missing names, remainder counted");
                }
            }
            check(none.Total > some.Total && some.Total > 0 && all.Total == 0 && some.Needed == none.Needed, product.Id + ": MissingCopies drives all three cases");
        }
        var deck = NativeShopPurchase.Products.First(p => p.Kind == "Deck" && !LocalShop.Default.Recipe(p.RecipeId).Cards.Any(card => LocalWelcomeGift.HistoricalCardIds.Contains(card.Id)));
        var deckRecipe = LocalShop.Default.Recipe(deck.RecipeId);
        var missingAll = LocalShop.Default.Library.MissingCopies(deckRecipe, RecipeLibrary.OwnedCount(new Dictionary<string, int>()));
        check(NativeShopDecks.Details(deck, deckRecipe, missingAll, 99).Contains("\nMissing cards: ") , "missing card names listed when they fit");
        using var profile = new LocalProfile(fresh("native-deck-details", 100000));
        string fromProfile = NativeShopDecks.Details(deck, deckRecipe, profile, profile.PurchasePrice(deck.Id));
        var ledger = LocalShop.Default.Library.MissingCopies(deckRecipe, RecipeLibrary.OwnedCount(profile.OwnedAnyVersionCollection));
        check(fromProfile == NativeShopDecks.Details(deck, deckRecipe, ledger, profile.PurchasePrice(deck.Id)), "profile text uses the ledger's MissingCopies");
        deckRecipe.Preview = new string('x', NativeShopDecks.DetailsLimit);
        foreach (int kitCap in new[] { 0, LocalWelcomeGift.KitGoldCap }) {
            string limited = NativeShopDecks.Details(deck, deckRecipe, missingAll, 99999, kitCap);
            check(limited.StartsWith(deckRecipe.Name + " — " + RecipeLibrary.Label(deckRecipe) + "\nYou own ")
                && limited.Length <= NativeShopDecks.DetailsLimit && !limited.Contains(deckRecipe.Preview)
                && (kitCap == 0 || limited.Contains("Deck kit applied")),
                "oversized preview is dropped while deck identity, summary and kit text remain");
        }
    }
}
