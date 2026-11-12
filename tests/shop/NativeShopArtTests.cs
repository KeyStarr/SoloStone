// Unity-free parts of the shop tile art, the sub-tab cradle and the open category (plugin/NativeShopArtMath.cs,
// NativeShopTabs.OpenTab). The Unity side (render, paint) is checked live.
static class NativeShopArtTests
{
    private const int W = NativeShopArtMath.PlateWidth, H = NativeShopArtMath.PlateHeight;

    private static byte[] Fill(int width, int height, Func<int, int, (byte r, byte g, byte b, byte a)> pixel)
    {
        var bytes = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y); int at = (y * width + x) * 4;
                bytes[at] = r; bytes[at + 1] = g; bytes[at + 2] = b; bytes[at + 3] = a;
            }
        return bytes;
    }
    // A board-like render: beige middle, darker coloured frame and bright "props" in the corners.
    private static byte[] Board(int width, int height) => Fill(width, height, (x, y) =>
        x < width / 10 || x > width * 9 / 10 || y < height / 10 || y > height * 9 / 10
            ? ((x + y) % 7 < 3 ? ((byte)220, (byte)120, (byte)40, (byte)255) : ((byte)70, (byte)90, (byte)140, (byte)0))
            : ((byte)205, (byte)180, (byte)130, (byte)255));

    internal static void Run(Action<bool, string> check)
    {
        // Plausibility: black or flat renders are never cached; a board-like one is.
        int rw = NativeShopArtMath.RenderWidthPixels, rh = NativeShopArtMath.RenderHeightPixels;
        check(rw == 797 && rh == 1024, "board render is a quarter-turned 15.8 x 12.3 window");
        check(!NativeShopArtMath.Plausible(Fill(64, 64, (x, y) => (0, 0, 0, 0)), 64, 64), "black render rejected");
        check(!NativeShopArtMath.Plausible(Fill(64, 64, (x, y) => (120, 120, 120, 255)), 64, 64), "flat render rejected");
        check(!NativeShopArtMath.Plausible(Fill(64, 64, (x, y) => x < 40 ? ((byte)0, (byte)0, (byte)0, (byte)255) : ((byte)250, (byte)250, (byte)250, (byte)255)), 64, 64),
            "mostly black render rejected");
        var board = Board(200, 260);
        check(NativeShopArtMath.Plausible(board, 200, 260), "board-like render kept");
        check(!NativeShopArtMath.Plausible(board, 200, 259), "size mismatch rejected");
        var pink = Board(200, 260);
        for (int i = 0; i < 200 * 260; i += 7) { pink[i * 4] = 255; pink[i * 4 + 1] = 0; pink[i * 4 + 2] = 255; }
        check(!NativeShopArtMath.Plausible(pink, 200, 260), "render with missing-shader magenta rejected");
        var speck = Board(200, 260); // 1% exact magenta, spread so the sampler sees it
        for (int i = 0; i < 200 * 260; i += 100) { speck[i * 4] = 255; speck[i * 4 + 1] = 0; speck[i * 4 + 2] = 255; }
        check(!NativeShopArtMath.Plausible(speck, 200, 260), "1% missing-shader magenta rejected");
        var purple = Fill(200, 260, (x, y) => (x + y) % 5 < 2 ? ((byte)230, (byte)60, (byte)220, (byte)255)
            : (x + y) % 5 < 4 ? ((byte)140, (byte)50, (byte)170, (byte)255) : ((byte)240, (byte)180, (byte)235, (byte)255));
        check(NativeShopArtMath.Plausible(purple, 200, 260), "a legitimately pink and purple board is kept");

        // Opaque: additive FX pixels keep their colour and become opaque.
        var fx = Fill(2, 1, (x, y) => (10, 200, 30, 0));
        NativeShopArtMath.Opaque(fx);
        check(fx[3] == 255 && fx[7] == 255 && fx[1] == 200, "alpha forced, colour kept");

        // Plate: fixed size; contain-fit and centred; transparent art leaves the gradient plate.
        NativeShopArtMath.Opaque(board);
        var plate = NativeShopArtMath.Plate(board, 200, 260, W, H);
        check(plate.Length == W * H * 4, "plate is 256 x 350 RGBA");
        // 200x260 into 256x350: scale min(1.28, 1.346) = 1.28 -> 256 x 332, centred vertically (9 rows of plate each side).
        int Lum(int x, int y) { int at = (y * W + x) * 4; return plate[at] + plate[at + 1] + plate[at + 2]; }
        check(Lum(W / 2, H / 2) > 400, "art at the centre");
        check(Lum(W / 2, 2) < 200 && Lum(W / 2, H - 3) < 200, "gradient plate above and below the art");
        var empty = NativeShopArtMath.Plate(Fill(10, 10, (x, y) => (255, 255, 255, 0)), 10, 10, W, H);
        check(empty[(H / 2 * W + W / 2) * 4] < 70, "fully transparent art draws nothing");
        var wide = NativeShopArtMath.Plate(Fill(400, 100, (x, y) => (250, 250, 250, 255)), 400, 100, W, H);
        check(wide[((H / 2) * W + 1) * 4] > 200 && wide[((H / 2 + 60) * W + W / 2) * 4] < 70, "landscape art: full width, margins above and below");

        // Cache: versioned header round trip; any other version, framing, size, magic or implausible pixels re-render.
        int key = NativeShopArtMath.FramingKey;
        check(key == NativeShopArtMath.Fnv(string.Join("|", new object[] { NativeShopArtMath.RenderVersion, W, H, NativeShopArtMath.ArtCentre,
            NativeShopArtMath.RenderWidth, NativeShopArtMath.AreaX, NativeShopArtMath.AreaZ, NativeShopArtMath.OffsetX, NativeShopArtMath.OffsetZ,
            NativeShopArtMath.TableX, NativeShopArtMath.TableY, NativeShopArtMath.TableZ, "quarter-turn" }
            .Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)))), "framing key covers version, plate and framing");
        check(NativeShopArtMath.Fnv("a") != NativeShopArtMath.Fnv("b") && NativeShopArtMath.Fnv("abc") == NativeShopArtMath.Fnv("abc"), "stable hash");
        var file = NativeShopArtMath.Encode(plate, key);
        var back = NativeShopArtMath.Decode(file, key, out string reason);
        check(back != null && reason == null && back.SequenceEqual(plate), "cache round trip");
        check(NativeShopArtMath.Decode(file, key + 1, out reason) == null && reason == "framing", "other framing re-renders");
        check(NativeShopArtMath.Decode(file.Take(file.Length - 1).ToArray(), key, out reason) == null && reason == "size", "truncated file re-renders");
        var badMagic = (byte[])file.Clone(); badMagic[0] ^= 1;
        check(NativeShopArtMath.Decode(badMagic, key, out reason) == null && reason == "format", "foreign file re-renders");
        var badDims = (byte[])file.Clone(); BitConverter.GetBytes(W - 1).CopyTo(badDims, 12);
        check(NativeShopArtMath.Decode(badDims, key, out reason) == null && reason == "dimensions", "other plate size re-renders");
        var dark = NativeShopArtMath.Encode(new byte[W * H * 4], key);
        check(NativeShopArtMath.Decode(dark, key, out reason) == null && reason == "pixels", "a black cached plate re-renders");
        check(NativeShopArtMath.Decode(null, key, out reason) == null, "missing file re-renders");
        bool threw = false; try { NativeShopArtMath.Encode(new byte[10], key); } catch (ArgumentException) { threw = true; }
        check(threw, "only whole plates are written");

        // Ticket mapping: the measured face window (u 0..0.493, v 0.326..1) lands on 0..1 of the art.
        var map = NativeShopArtMath.TicketTransform(0f, 0.326f, 0.493f, 1f, 1f, 1f, 0f, 0f);
        float U(float u) => u * map[0] + map[2];
        float V(float v) => v * map[1] + map[3];
        check(Math.Abs(U(0f)) < 1e-4 && Math.Abs(U(0.493f) - 1) < 1e-4 && Math.Abs(V(0.326f)) < 1e-4 && Math.Abs(V(1f) - 1) < 1e-4, "ticket face maps onto the art");
        var scaled = NativeShopArtMath.TicketTransform(0f, 0f, 1f, 1f, 2f, 2f, 0.5f, 0.5f);
        check(Math.Abs(0f * scaled[0] + scaled[2]) < 1e-4 && Math.Abs(1f * scaled[0] + scaled[2] - 1) < 1e-4, "native texture transform folded in");

        // Cradle: hidden only in the local shop, with sockets left empty, unless the cfg keeps it.
        check(NativeShopArtMath.HideCradle(true, "hide", 3, 6) && NativeShopArtMath.HideCradle(true, null, 2, 6), "fewer sub-tabs than sockets: hidden");
        check(!NativeShopArtMath.HideCradle(true, "hide", 6, 6), "every socket used: kept");
        check(!NativeShopArtMath.HideCradle(false, "hide", 3, 6), "shop disabled: native");
        check(!NativeShopArtMath.HideCradle(true, "Native", 3, 6), "cfg native: kept");
        check(!NativeShopArtMath.HideCradle(true, "hide", 0, 0) && !NativeShopArtMath.HideCradle(true, "hide", -1, 6), "bar not loaded: untouched");

        // Queue: shown tiles first (on screen before off screen), one job at a time, back to back while the shop is open.
        var queue = new NativeShopArtQueue();
        queue.SetPrewarm(new[] { 10, 11, 12 });
        check(queue.Next(true, false, true, 0f) == -1, "open shop with no shown tiles: nothing (no pre-warm)");
        check(queue.Next(false, false, true, 0f) == -1, "closed, not idle: nothing");
        check(queue.Next(false, true, false, 0f) == -1, "idle hub with recent input: nothing");
        queue.Request(5, false); queue.Request(6, true); queue.Request(5, false);
        check(queue.Next(true, false, false, 0f) == 6 && queue.Busy, "on-screen tile first, input does not hold the open shop back");
        check(queue.Next(true, false, true, 0f) == -1, "one job at a time");
        queue.Finish(6);
        check(!queue.Busy && queue.Has(6), "finished");
        queue.Request(6, true);
        check(queue.Next(true, false, false, 0.1f) == 5, "finished boards are not requested again; off-screen next, back to back");
        queue.Finish(5);
        // Review M-R1: requests left when the shop closes follow the pre-warm gate (idle, quiet, spaced).
        queue.Request(7, true); queue.Request(8, false);
        check(queue.Next(false, false, true, 5f) == -1, "closed shop, not idle: leftover requests wait");
        check(queue.Next(false, true, false, 5f) == -1, "closed shop, input: leftover requests wait");
        check(queue.Next(false, true, true, 5f) == 7, "closed shop, quiet idle hub: leftover requests first");
        queue.Finish(7);
        check(queue.Next(false, true, true, 6f) == -1, "spacing applies to leftovers");
        check(queue.Next(false, true, true, 6.5f) == 8, "next leftover after the spacing");
        queue.Finish(8);
        check(queue.Next(false, true, true, 8.5f) == 10, "then the pre-warm list in order");
        // Review L-R2/L-R3: input or a match parks the job; it comes back first, after the spacing.
        queue.Park(10, 9f);
        check(!queue.Busy && !queue.Has(10), "parked, not done");
        check(queue.Next(false, true, true, 9.5f) == -1 && queue.Next(false, true, true, 10.5f) == 10, "parked board is next, after the spacing");
        // Review M-R2: a board that errors is given up for the session, the rest go on.
        queue.Finish(10);
        queue.Request(10, true);
        check(queue.Next(true, false, true, 11f) == -1 && queue.Has(10), "given-up board never retried");
        check(queue.Next(false, true, true, 12f) == 11, "the others continue");
        queue.Finish(11);
        queue.Request(12, false);
        check(queue.Next(true, true, true, 20f) == 12, "a shown tile jumps the pre-warm list");
        queue.Finish(12);
        check(queue.Next(false, true, true, 40f) == -1, "all made: nothing left");
        queue.Reset();
        check(!queue.Busy && !queue.Has(6) && !queue.Has(10), "reset (TileArt mode change) forgets everything");
        check(NativeShopArtQueue.HeavyAllowed(true, false, false) && NativeShopArtQueue.HeavyAllowed(false, true, true)
            && !NativeShopArtQueue.HeavyAllowed(false, true, false) && !NativeShopArtQueue.HeavyAllowed(false, false, true),
            "heavy stages: open shop, or a quiet idle hub");
        check(!NativeShopArtQueue.Quiet(10f, 7f) && NativeShopArtQueue.Quiet(11f, 7f), "pre-warm waits for 4 s without input");

        // Open category: the first one with products.
        check(NativeShopTabs.Ids.SequenceEqual(new[] { "cards", "decks_1", "decks_2", "skins", "themes" }), "stable category ids in drawer order");
        check(NativeShopTabs.KitBadgeTabs.SetEquals(new[] { "decks_1", "decks_2" }), "kit badges only on Decks I and Decks II categories");
        check(NativeShopTabs.DeckClasses.Select(c => c.Hero).SequenceEqual(new[] {
            "HERO_11", "HERO_10", "HERO_06", "HERO_05", "HERO_08", "HERO_04", "HERO_09", "HERO_03", "HERO_02", "HERO_07", "HERO_01"
        }) && NativeShopTabs.DeckClasses.Select(c => c.Tab).SequenceEqual(Enumerable.Repeat("decks_1", 6).Concat(Enumerable.Repeat("decks_2", 5))), "class hero order and six/five split");
        check(NativeShopTabs.DeckClasses.All(c => c.Id == "deck_" + c.Label.ToLowerInvariant().Replace(" ", "")), "class sub-tab ids match labels");
        check(NativeShopTabs.SubTabs.SequenceEqual(new[] { "packs" }.Concat(NativeShopTabs.DeckClasses.Select(c => c.Id))
            .Concat(new[] { "hero_skins", "card_backs", "coin_list", "pet_list", "board_list", "theme_list" })), "stable sub-tab ids");
        check(NativeShopTabs.OpenTab(new[] { "Board", "Pack" }) == NativeShopTabs.Cards, "cards first");
        check(NativeShopTabs.OpenTab(new[] { "Deck", "Pack" }) == NativeShopTabs.Cards, "packs before decks");
        check(NativeShopTabs.OpenTab(new[] { "Board", "Deck" }) == "decks_1", "decks open Decks I");
        check(NativeShopTabs.OpenTab(new[] { "Deck", "Recipe" }, new[] { "HERO_09", "HERO_01" }) == "decks_2", "only Priest-Warrior decks open Decks II");
        check(NativeShopTabs.OpenTab(new[] { "Recipe" }, new[] { "HERO_09", "HERO_11" }) == "decks_1", "any Decks I class opens Decks I");
        check(NativeShopTabs.OpenTab(new[] { "HeroSkin", "Recipe" }) == "decks_1", "recipes open Decks I");
        check(NativeShopTabs.OpenTab(new[] { "HubTheme", "CardBack" }) == NativeShopTabs.Skins, "no cards: skins");
        check(NativeShopTabs.OpenTab(new[] { "Board" }) == NativeShopTabs.Themes, "only themes");
        check(NativeShopTabs.OpenTab(Array.Empty<string>()) == NativeShopTabs.Cards && NativeShopTabs.OpenTab(null) == NativeShopTabs.Cards, "empty catalogue: cards");
        var messages = new List<string>();
        try {
            foreach (string style in new[] { "standard", "standardsingle" }) {
                NativeShopLayout.ConfigureDeckRowStyle(style, messages.Add);
                check(NativeShopLayout.DeckRowStyle == style && messages.Count == 0, "accepted deck row style: " + style);
            }
            foreach (string invalid in new[] { "", "dense", "STANDARD", null }) {
                NativeShopLayout.ConfigureDeckRowStyle("standardsingle", messages.Add);
                int before = messages.Count;
                NativeShopLayout.ConfigureDeckRowStyle(invalid, messages.Add);
                check(NativeShopLayout.DeckRowStyle == NativeShopLayout.DefaultDeckRowStyle && messages.Count == before + 1, "invalid deck row style falls back and logs");
            }
            check(NativeShopLayout.DefaultDeckRowStyle == "standardsingle", "one tile per deck: four decks per row by default");
            foreach (string blank in new[] { "", " ", null }) { NativeShopLayout.ConfigureDeckHeaders(blank); check(NativeShopLayout.DeckHeaders == "header_plate", "blank deck headers fall back"); }
            NativeShopLayout.ConfigureDeckHeaders(" None "); check(NativeShopLayout.DeckHeaders == "none", "deck headers can be switched off");
            // [Shop] RecipeTilePlate (NativeShopTilePrices placement knobs).
            var recipePlate = NativeShopArtMath.ParseRecipePlate(null);
            check(recipePlate.Enabled && recipePlate.Lift == 2 && recipePlate.X == 0f && recipePlate.Y == 0.85f && recipePlate.Scale == 1f
                && recipePlate.Label("100") == "100 Recipe" && recipePlate.Key == NativeShopArtMath.ParseRecipePlate(NativeShopArtMath.DefaultRecipePlate).Key
                && recipePlate.Key == NativeShopArtMath.ParseRecipePlate("  ").Key, "recipe plate defaults");
            check(!NativeShopArtMath.ParseRecipePlate(" NONE ").Enabled && NativeShopArtMath.ParseRecipePlate("none").Key == "none", "recipe plate off");
            var plateLog = new List<string>();
            var tuned = NativeShopArtMath.ParseRecipePlate("lift=1; x=-0.55;Y=0 ; scale=0.8; text=Recipe {0}; queue=7", plateLog.Add);
            check(tuned.Queue == 7 && recipePlate.Queue == -1 && tuned.Lift == 1 && tuned.X == -0.55f && tuned.Y == 0f && tuned.Scale == 0.8f && tuned.Label("100") == "Recipe 100" && plateLog.Count == 0,
                "recipe plate knobs parse (invariant, case-insensitive keys)");
            var bad = NativeShopArtMath.ParseRecipePlate("lift=1.5; lift=9; x=NaN; y=11; scale=3; text=Recipe; text={1}; text={0} {; colour=red; x; queue=-2; queue=0.5", plateLog.Add);
            check(bad.Key == recipePlate.Key && plateLog.Count == 12, "bad recipe plate entries keep defaults and are each logged");
            NativeShopLayout.ConfigureDeckHeaders("Header_Plate, , watermark "); check(NativeShopLayout.DeckHeaders == "header_plate,watermark", "deck header options trimmed and lower-cased");
        }
        finally { NativeShopLayout.DeckRowStyle = NativeShopLayout.DefaultDeckRowStyle; NativeShopLayout.DeckHeaders = NativeShopLayout.DefaultDeckHeaders; }
    }
}
