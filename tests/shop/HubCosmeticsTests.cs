using System.Text.Json.Nodes;
using System.Xml.Serialization;

// Owner 2026-10-25: main-menu themes and game boards are shop cosmetics with a "Random owned" choice.
internal static class HubCosmeticsTests
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(file)!; }
    private static void Write(string path, LocalProfileData data) { using var file = File.Create(path); Serializer.Serialize(file, data); }

    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        string Fresh(string name, int gold = 100000)
        {
            string path = Path.Combine(root, "hub-" + name + ".xml");
            using (var p = new LocalProfile(path)) p.SelectCardBack(0);
            var data = Read(path); data.GoldBalance = gold; Write(path, data); return path;
        }
        var shop = LocalShop.Default;
        var themes = shop.Products.Where(p => p.Kind == "HubTheme").ToArray();
        var boards = shop.Products.Where(p => p.Kind == "Board").ToArray();
        check(themes.Where(p => p.Price > 0).Select(p => p.HubTheme).OrderBy(k => k).SequenceEqual(new[] { "ANNIVERSARY", "CATA",
            "CATCH_UP_WILD_WEST", "DK_LAUNCH", "FIRE_FESTIVAL", "HALLOWS_END", "STARCRAFT", "VIOLET", "WINTER_VEIL" })
            && themes.Where(p => p.Price > 0).All(p => p.Price == 1000 && p.Id == "theme-" + p.HubTheme), "nine themes for sale at 1000 gold");
        check(themes.Where(p => p.Price == 0).Select(p => p.HubTheme).SequenceEqual(new[] { "random", "CLASSIC" }) && !themes.Any(p => p.HubTheme == "BLACKEMPIRE" || p.HubTheme == "TEST_EVENT"),
            "Random owned and Classic free; Black Empire and TEST_EVENT never sold");
        check(boards.Where(p => p.Price == 0).Select(p => p.Board!.Value).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
            && boards.Where(p => p.Price > 0).All(p => p.Price == 500 && p.Id == "board-" + p.Board && p.Board > 7), "boards 1-7 free, others 500 gold");
        // Only boards verified in a real match are sold, and every verified board is for sale.
        JsonArray raw;
        using (var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!) raw = JsonNode.Parse(source)!["Products"]!.AsArray();
        var sold = raw.Where(p => (string)p!["Kind"]! == "Board" && (int)p!["Price"]! > 0 && !(bool)p!["verify"]!).Select(p => (int)p!["Board"]!).ToArray();
        check(sold.All(id => BoardChoice.Allowlist.Contains(id)) && BoardChoice.Allowlist.Where(id => id > 7).All(sold.Contains)
            && BoardChoice.FreeBoards.SequenceEqual(LocalHubCosmetics.DefaultBoards), "sold boards are exactly the verified allowlist");
        check(!boards.Any(p => new[] { 71, 85, 88, 69, 79, 81, 87 }.Contains(p.Board!.Value) || p.Board >= 102 && p.Board <= 105
            || p.Board == 119 || p.Board >= 121 && p.Board <= 125), "Battlegrounds, Mercenaries, tutorial and green-screen boards excluded");

        // Random owned theme: uniform among owned looks; a re-roll avoids the previous launch's look.
        check(LocalHubCosmetics.RollTheme(new[] { "CLASSIC" }, "CLASSIC", 7) == "CLASSIC", "only Classic owned rolls Classic");
        check(Enumerable.Range(0, 50).All(roll => LocalHubCosmetics.RollTheme(new[] { "CLASSIC", "VIOLET" }, "VIOLET", roll) == "CLASSIC")
            && Enumerable.Range(0, 50).All(roll => LocalHubCosmetics.RollTheme(new[] { "CLASSIC", "VIOLET" }, "CLASSIC", roll) == "VIOLET"),
            "two owned looks alternate across launches");
        var rolls = Enumerable.Range(0, 300).Select(roll => LocalHubCosmetics.RollTheme(new[] { "CLASSIC", "VIOLET", "CATA" }, "", roll)).ToArray();
        check(rolls.Distinct().Count() == 3 && rolls.GroupBy(k => k).All(g => g.Count() == 100), "uniform roll over owned looks");

        // Board for each match: a fixed owned verified board always loads; Random owned picks from the seed.
        int[] present = Enumerable.Range(1, 200).ToArray();
        check(BoardChoice.ForPlayer(5, present, 0, 3, new int[0]) == 3, "a fixed free board always loads");
        check(BoardChoice.ForPlayer(5, present, 0, 13, new int[0]) == BoardChoice.ForPlayer(5, present, 0, 0, new int[0]), "an unowned selection falls back to random owned");
        var picks = Enumerable.Range(0, 400).Select(seed => BoardChoice.ForPlayer(seed, present, 0, 0, new int[0])).ToArray();
        check(picks.All(LocalHubCosmetics.DefaultBoards.Contains) && picks.Distinct().Count() == 7, "random owned varies across matches among owned boards");
        check(Enumerable.Range(0, 400).All(seed => BoardChoice.ForPlayer(seed, present, 0, 0, new int[0]) == picks[seed]), "same seed, same board");
        check(BoardChoice.ForPlayer(5, present, 20, 3, new int[0]) == 20, "verification force wins");
        int bought = BoardChoice.Allowlist.FirstOrDefault(id => id > 7);
        if (bought > 0) {
            check(BoardChoice.ForPlayer(5, present, 0, bought, new[] { bought }) == bought, "a fixed bought board always loads");
            check(Enumerable.Range(0, 400).Any(seed => BoardChoice.ForPlayer(seed, present, 0, 0, new[] { bought }) == bought)
                && !Enumerable.Range(0, 400).Any(seed => BoardChoice.ForPlayer(seed, present, 0, 0, new int[0]) == bought), "random owned includes bought boards only");
            check(BoardChoice.ForPlayer(5, present.Where(id => id != bought), 0, bought, new[] { bought }) != bought, "a missing board never loads");
        }

        // Fresh profile: Classic and the random owned board; the free defaults are usable without receipts.
        string path = Fresh("fresh");
        using (var p = new LocalProfile(path)) {
            check(p.SelectedHubTheme == "CLASSIC" && p.SelectedBoard == 0 && p.OwnedHubThemes.Count == 0 && p.OwnedBoards.Count == 0
                && p.UsableHubThemes.SequenceEqual(new[] { "CLASSIC" }) && p.UsableBoards.SequenceEqual(LocalHubCosmetics.DefaultBoards), "fresh theme and board defaults");
            p.SelectBoard(4); p.SelectHubTheme("random");
        }
        using (var p = new LocalProfile(path)) check(p.SelectedBoard == 4 && p.SelectedHubTheme == "random", "selection persists across restart");
        check(Read(path).Version == 15, "new profiles use V15");

        // Buying equips; gold is taken once; ownership and selection survive restart; replay never charges twice.
        var theme = themes.First(p => p.Price > 0 && !p.verify);
        var board = boards.FirstOrDefault(p => p.Price > 0 && !p.verify) ?? boards.First(p => p.Price > 0);
        using (var p = new LocalProfile(path)) {
            var receipt = p.Purchase(theme.Id, 1, 1000, "theme-1");
            check(receipt.Granted.HubTheme == theme.HubTheme && p.Gold == 99000 && p.SelectedHubTheme == theme.HubTheme
                && p.OwnsHubTheme(theme.HubTheme) && p.UsableHubThemes.Contains(theme.HubTheme), "buying a theme equips it");
            check(p.Purchase(theme.Id, 1, 1000, "theme-1").Sequence == receipt.Sequence && p.Gold == 99000, "theme replay charges once");
            reject(() => p.Purchase(theme.Id, 1, 1000, "theme-2"), "owned theme refused");
            p.SelectHubTheme("CLASSIC");
            check(p.SelectedHubTheme == "CLASSIC" && p.OwnsHubTheme(theme.HubTheme), "re-equip Classic keeps ownership");
            p.SelectHubTheme(theme.HubTheme);
        }
        using (var p = new LocalProfile(path))
            check(p.SelectedHubTheme == theme.HubTheme && p.Gold == 99000 && p.OwnedHubThemes.SequenceEqual(new[] { theme.HubTheme }), "theme survives restart");
        using (var fixture = new CosmeticsFixture())
        using (var p = new LocalProfile(path)) {
            var receipt = p.Purchase(board.Id, 1, 500, "board-1");
            check(receipt.Granted.Board == board.Board && p.Gold == 98500 && p.SelectedBoard == board.Board && p.UsableBoards.Contains(board.Board!.Value), "buying a board equips it");
            p.SelectBoard(0);
        }
        using (var fixture = new CosmeticsFixture())
        using (var p = new LocalProfile(path)) check(p.SelectedBoard == 0 && p.OwnsBoard(board.Board!.Value) && p.Gold == 98500, "board survives restart");

        // A board later withdrawn from sale (or found broken) keeps its receipt valid.
        var retired = WithoutProduct(board.Id);
        var previous = LocalShop.TestOverride;
        LocalShop.TestOverride = retired;
        try {
            using (var p = new LocalProfile(path)) check(p.OwnsBoard(board.Board!.Value), "retired board receipt still loads");
        }
        finally { LocalShop.TestOverride = previous; }

        // Tampered current ledgers are refused; game-written ones always load.
        foreach (var (name, change) in new (string, Action<LocalProfileData>)[] {
            ("unowned theme selection", d => d.SelectedHubTheme = "STARCRAFT"),
            ("unknown theme selection", d => d.SelectedHubTheme = "TEST_EVENT"),
            ("missing theme selection", d => d.SelectedHubTheme = null),
            ("unowned board selection", d => d.SelectedBoard = 142),
            ("free board owned", d => d.OwnedBoards.Add(2)),
            ("theme without receipt", d => d.OwnedHubThemes.Add("STARCRAFT")),
            ("duplicate theme", d => d.OwnedHubThemes.Add(theme.HubTheme)),
        }) {
            string bad = Path.Combine(root, "hub-bad-" + name.Replace(' ', '-') + ".xml");
            var data = Read(path); change(data); Write(bad, data);
            reject(() => { using var fixture = new CosmeticsFixture(); new LocalProfile(bad).Dispose(); }, "tampered current profile refused: " + name);
        }
    }

    private static LocalShop WithoutProduct(string id)
    {
        var original = LocalShop.Default;
        using var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!;
        var root = JsonNode.Parse(source)!;
        var products = root["Products"]!.AsArray();
        products.Remove(products.Single(p => (string)p!["Id"]! == id));
        using var catalog = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString().Replace("\"verify\":true", "\"verify\":false")));
        return LocalShop.Load(catalog, () => original.Library);
    }
}
