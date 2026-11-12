using System.Xml.Serialization;
using MiniJSON;

internal static class ShopControlTests
{
    internal static void Run(string root)
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        void Refused(Action action, string reason) {
            try { action(); } catch (InvalidOperationException error) {
                Check(error.Message.Contains(reason, StringComparison.OrdinalIgnoreCase), "wrong refusal: " + error.Message); return;
            }
            throw new Exception("Accepted " + reason);
        }
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        string Fresh(string name, int gold = 100000) {
            string path = Path.Combine(root, "control-" + name + ".xml");
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = gold;
            WelcomeGiftTests.OwnGrantOnly(data);
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            return path;
        }
        // Every item is purchased by Program.cs; catalogue/receipt wire shape needs one skin per class.
        // Free themes and boards (Random owned, Classic, boards 1-7) are owned by everyone and never sold.
        using (var profile = new LocalProfile(Fresh("free-looks"))) {
            var rows = ((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().ToArray();
            foreach (var free in LocalShop.Default.Products.Where(p => (p.Kind == "HubTheme" || p.Kind == "Board" || p.Kind == "Pet") && p.Price == 0)) {
                var row = rows.Single(r => (string)r["id"] == free.Id);
                Check((bool)row["owned"] && !(bool)row["purchasable"], "free look owned, not sold " + free.Id);
                Refused(() => TestControlShop.Purchase(profile, "free-" + free.Id, free.Id, 1, null, true), "owned");
            }
            Check((string)TestControlShop.Catalog(profile, true)["selectedHubTheme"] == "CLASSIC"
                && Convert.ToInt32(TestControlShop.Catalog(profile, true)["selectedBoard"]) == 0, "catalogue reports Classic theme and random board");
        }
        // A withheld recipe (RecipePending without a fallback, dependency not landed) is never offered and never sold.
        foreach (var product in LocalShop.Default.Products.Where(LocalShop.Default.Withheld)) {
            string path = Fresh(product.Id);
            using (var profile = new LocalProfile(path)) {
                var row = ((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().Single(r => (string)r["id"] == product.Id);
                Check(!(bool)row["purchasable"], "withheld product not purchasable " + product.Id);
                Refused(() => TestControlShop.Purchase(profile, "withheld", product.Id, 1, null, true), "Unknown shop recipe");
            }
        }
        foreach (var product in LocalShop.Default.Products.Where(p => !((p.Kind == "HubTheme" || p.Kind == "Board" || p.Kind == "Pet") && p.Price == 0)
                && !LocalShop.Default.Withheld(p))
            .GroupBy(p => p.Kind == "HeroSkin" ? p.Class : p.Id).Select(g => g.First())) {
            string path = Fresh(product.Id);
            using (var profile = new LocalProfile(path)) {
                var before = File.ReadAllBytes(path);
                var rows = (JsonList)TestControlShop.Catalog(profile, true)["products"];
                var row = rows.Cast<JsonNode>().Single(r => (string)r["id"] == product.Id);
                int price = profile.PurchasePrice(product.Id);
                Check(Convert.ToInt32(row["price"]) == price && (bool)row["purchasable"] && !(bool)row["owned"], "catalogue price and eligibility " + product.Id);
                Check(File.ReadAllBytes(path).SequenceEqual(before), "catalogue must not persist");
                var receipt = TestControlShop.Purchase(profile, "action-1", product.Id, 1, null, true);
                Check(receipt.Sequence == 1 && receipt.RequestId == "action-1" && receipt.GoldPaid == price, "command receipt " + product.Id);
                var json = (JsonNode)Json.Deserialize(Json.Serialize(TestControlShop.Receipt(receipt, profile.Gold)));
                var granted = (JsonNode)json["granted"];
                Check(Convert.ToInt64(json["sequence"]) == 1 && (string)json["requestId"] == "action-1"
                    && Convert.ToInt32(json["goldPaid"]) == price && Convert.ToInt32(json["goldAfter"]) == 100000 - price, "wire receipt totals");
                Check(((JsonList)granted["packs"]).Count == (product.Kind == "Pack" ? 1 : 0)
                    && ((JsonList)granted["cards"]).Count == receipt.Granted.Cards.Count
                    && (string)granted["recipeId"] == product.RecipeId && (string)granted["heroSkin"] == product.HeroSkin,
                    "wire receipt grants " + product.Id);
                if (product.Kind == "Deck") Check(Convert.ToInt64(granted["deckId"]) == profile.CustomDecks.Last().Id, "wire deck ID");
                if (product.Kind == "CardBack") Check(Convert.ToInt32(granted["cardBack"]) == product.CardBack, "wire card back");
                if (product.Kind == "HubTheme") Check((string)granted["hubTheme"] == product.HubTheme && profile.SelectedHubTheme == product.HubTheme, "wire theme, equipped");
                if (product.Kind == "Board") Check(Convert.ToInt32(granted["board"]) == product.Board && profile.SelectedBoard == product.Board, "wire board, equipped");
                byte[] committed = File.ReadAllBytes(path); int gold = profile.Gold;
                var replay = TestControlShop.Purchase(profile, "action-1", product.Id, 1, null, false);
                Check(replay.Sequence == 1 && profile.Gold == gold && File.ReadAllBytes(path).SequenceEqual(committed), "same action never charges twice " + product.Id);
                if (product.Kind == "Deck") Check(profile.PurchasePrice(product.Id) == 0, "deck replay covers changed price");
                Refused(() => TestControlShop.Purchase(profile, "action-1", product.Id, 2, null, true), "Conflicting");
                Refused(() => TestControlShop.Purchase(profile, "action-1", "unknown", 1, null, true), "Conflicting");
                Refused(() => TestControlShop.Purchase(profile, "action-1", product.Id, 1, "other-kit", true), "Conflicting");
                if (product.Kind != "Pack") {
                    Refused(() => TestControlShop.Purchase(profile, "new-action", product.Id, 1, null, true), "owned");
                    var owned = ((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().Single(r => (string)r["id"] == product.Id);
                    Check((bool)owned["owned"] && !(bool)owned["purchasable"], "owned catalogue row");
                }
            }
            using (var profile = new LocalProfile(path)) {
                int gold = profile.Gold;
                Check(TestControlShop.Purchase(profile, "action-1", product.Id, 1, null, true).Sequence == 1 && profile.Gold == gold, "replay after restart " + product.Id);
            }
        }
        string poor = Fresh("poor", 0);
        using (var profile = new LocalProfile(poor)) {
            byte[] before = File.ReadAllBytes(poor);
            Refused(() => TestControlShop.Purchase(profile, "unknown", "unknown", 1, null, true), "Unknown shop product");
            Refused(() => TestControlShop.Purchase(profile, "poor", "pack-standard", 1, null, true), "gold");
            Refused(() => TestControlShop.Purchase(profile, "match", "pack-standard", 1, null, false), "HUB/menu");
            Check(((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().All(r => !(bool)r["purchasable"]), "poor catalogue");
            Check(File.ReadAllBytes(poor).SequenceEqual(before), "refusals preserve bytes");
        }
        string kitPath = Fresh("kit", 0);
        LocalProfileData kitData;
        using (var file = File.OpenRead(kitPath)) kitData = (LocalProfileData)serializer.Deserialize(file)!;
        kitData.DeckKits.Add(new LocalDeckKit { Id = "kit-1", MilestoneId = "Silver", GoldCap = 100000 });
        using (var file = File.Create(kitPath)) serializer.Serialize(file, kitData);
        using (var profile = new LocalProfile(kitPath)) {
            var receipt = TestControlShop.Purchase(profile, "kit-action", "deck-manastorm-mage", 1, "kit-1", true);
            Check(receipt.GoldPaid == 0 && receipt.PriceBeforeKit > 0 && receipt.KitId == "kit-1", "handler applies kit before affordability");
            byte[] before = File.ReadAllBytes(kitPath);
            Check(TestControlShop.Purchase(profile, "kit-action", "deck-manastorm-mage", 1, "kit-1", true).Sequence == 1
                && File.ReadAllBytes(kitPath).SequenceEqual(before), "redeemed kit replay");
        }
        string matchPath = Fresh("match");
        using (var profile = new LocalProfile(matchPath)) {
            var match = profile.Begin("mage", "mage", 1);
            byte[] before = File.ReadAllBytes(matchPath);
            Refused(() => TestControlShop.Purchase(profile, "mid-match", "pack-standard", 1, null, true), "pending match");
            Check(File.ReadAllBytes(matchPath).SequenceEqual(before), "pending match refusal preserves bytes");
            profile.Settle(match.Id, "Lost", 1, true);
            Refused(() => TestControlShop.Purchase(profile, "rewards", "pack-standard", 1, null, true), "rewards");
        }
        string pending = Fresh("pending");
        using (var profile = new LocalProfile(pending)) {
            TestControlShop.Purchase(profile, "packs", "pack-standard", 3, null, true);
            profile.BeginNativePack();
            byte[] before = File.ReadAllBytes(pending);
            Check(TestControlShop.Purchase(profile, "packs", "pack-standard", 3, null, false).Sequence == 1, "replay survives pending reveal");
            foreach (var product in LocalShop.Default.Products.Where(p => !LocalShop.Default.Withheld(p)))
                Refused(() => TestControlShop.Purchase(profile, "pending-" + product.Id, product.Id, 1, null, true), "pack reveal");
            Check(((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().All(r => !(bool)r["purchasable"]), "pending catalogue");
            Check(File.ReadAllBytes(pending).SequenceEqual(before), "pending refusals preserve bytes");
        }
        Console.WriteLine("PASS shop test-control: catalogue, receipt wire format, action ID replay/restart, kits and mutation refusals");
    }
}
