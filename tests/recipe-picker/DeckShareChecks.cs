using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Sharing a deck (#220): the client's own Copy writes the code; DeckCodeText reads it (log, tests, text pasted back from
// Discord) and the import plans it. Client-free: Writer below mirrors the client's version-1 writer (ShareableDeck), field by
// field, so a code it writes is a code the client writes for the same deck.
internal static class DeckShareChecks
{
    private static int checks;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }

    // The client's layout: 0, version 1, format, 1 hero, then cards with one copy, two copies, more (card, count); then 1 and the
    // sideboard cards with one copy, two copies (card, owner), more (count, card, owner), or 0. Each list sorted by card dbId.
    internal static string Write(int format, int hero, IEnumerable<(int dbId, int qty)> cards, IEnumerable<(int dbId, int qty, int owner)> sideboards = null)
    {
        var main = cards.ToList();
        var side = (sideboards ?? Enumerable.Empty<(int, int, int)>()).ToList();
        var bytes = new List<byte> { 0 };
        void Varint(long value) { ulong rest = (ulong)value; do { byte next = (byte)(rest & 0x7f); rest >>= 7; bytes.Add(rest != 0 ? (byte)(next | 0x80) : next); } while (rest != 0); }
        Varint(1); Varint(format); Varint(1); Varint(hero);
        foreach (int copies in new[] { 1, 2 })
        {
            var rows = main.Where(card => card.qty == copies).OrderBy(card => card.dbId).ToList();
            Varint(rows.Count); foreach (var row in rows) Varint(row.dbId);
        }
        var many = main.Where(card => card.qty > 2).OrderBy(card => card.dbId).ToList();
        Varint(many.Count); foreach (var row in many) { Varint(row.dbId); Varint(row.qty); }
        if (side.Count == 0) bytes.Add(0);
        else
        {
            bytes.Add(1);
            foreach (int copies in new[] { 1, 2 })
            {
                var rows = side.Where(card => card.qty == copies).OrderBy(card => card.dbId).ToList();
                Varint(rows.Count); foreach (var row in rows) { Varint(row.dbId); Varint(row.owner); }
            }
            var more = side.Where(card => card.qty > 2).OrderBy(card => card.dbId).ToList();
            Varint(more.Count); foreach (var row in more) { Varint(row.qty); Varint(row.dbId); Varint(row.owner); }
        }
        return Convert.ToBase64String(bytes.ToArray());
    }

    // The text the client's Copy puts on the clipboard (GetDeckStringWithComments), in English.
    private static string RetailText(string name, string code) => "### " + name + "\n# Class: Paladin\n# Format: Standard\n# Year of the Pegasus\n#\n"
        + "# 2x (1) Some Card\n# 1x (5) Another Card\n#   1x (2) Sideboard Card\n#\n" + code + "\n#\n# To use this deck, copy it to your clipboard and create a new deck in Hearthstone\n";

    private static IEnumerable<(int dbId, int qty)> Summed(IEnumerable<(int dbId, int qty)> rows) =>
        rows.GroupBy(row => row.dbId).Select(group => (group.Key, group.Sum(row => row.qty))).OrderBy(row => row.Key);
    private static int Db(string id) => Cards.FromId(id).AssetId;
    private static string Id(int dbId) => Cards.FromAssetId(dbId)?.Id;

    internal static int Run()
    {
        // A real retail code from before sideboards existed (no sideboard byte at all): Hunter (Rexxar, dbId 31), Standard.
        var retail = DeckCodeText.TryDecode("AAECAR8GxwPJBLsFmQfZB/gIDI0B2AGoArUDhwSSBe0G6wfbCe0JgQr+DAA=");
        Check(retail != null && retail.Hero == 31 && retail.Format == 2 && retail.CardCount == 30 && retail.Sideboards.Count == 0
            && retail.Cards.Where(card => card.qty == 1).Select(card => card.dbId).SequenceEqual(new[] { 455, 585, 699, 921, 985, 1144 })
            && retail.Cards.Where(card => card.qty == 2).Select(card => card.dbId).SequenceEqual(new[] { 141, 216, 296, 437, 519, 658, 877, 1003, 1243, 1261, 1281, 1662 }),
            "a retail code decodes: hero, format, six singles and twelve pairs");
        Check(Convert.FromBase64String(Write(2, 31, retail.Cards)).SequenceEqual(
                Convert.FromBase64String("AAECAR8GxwPJBLsFmQfZB/gIDI0B2AGoArUDhwSSBe0G6wfbCe0JgQr+DAA=").Concat(new byte[] { 0 })),
            "the writer reproduces the retail code byte for byte, plus the client's no-sideboard byte");

        // Retail codes with sideboards, from the public HearthSim python-hearthstone deckstring tests (MIT): E.T.C. Band Manager
        // (90749, Wild Priest), a Standard deck whose 102983 owns three picks, and the library's canonical code with 3-copy cards.
        foreach (var (vector, format, hero, count, cardRows, sideRows) in new[] {
            ("AAEBAZCaBgjlsASotgSX7wTvkQXipAX9xAXPxgXGxwUQvp8EobYElrcE+dsEuNwEutwE9vAEhoMFopkF4KQFlMQFu8QFu8cFuJ4Gz54G0Z4GAAED8J8E/cQFuNkE/cQF/+EE/cQFAAA=",
                1, 101648, 40, new[] { (90749, 1), (102223, 2), (69566, 2) }, new[] { (69616, 1, 90749), (76984, 1, 90749), (78079, 1, 90749) }),
            ("AAECAfHhBAiy9wTt/wWLkgaYlQb/lwbHpAbT5QbW5QYLh/YE88gF8OgFl5UGkZcGkqAGurEGvLEG9rEGi7cG1+UGAAED87MGx6QG9rMGx6QG6N4Gx6QGAAA=",
                2, 78065, 30, new[] { (102983, 1), (111319, 2) }, new[] { (104947, 1, 102983), (104950, 1, 102983), (110440, 1, 102983) }),
            ("AAEBAR8DAQQGAwIFBwIDAwkDAQMKAgEDCAMAAA==", 1, 31, 15, new[] { (3, 3), (9, 3), (2, 2) }, new[] { (1, 1, 3), (8, 1, 3), (10, 1, 2) }) })
        {
            var sample = DeckCodeText.TryDecode(vector);
            Check(sample != null && sample.Format == format && sample.Hero == hero && sample.CardCount == count && cardRows.All(sample.Cards.Contains)
                && sample.Sideboards.OrderBy(card => card.owner).ThenBy(card => card.dbId).SequenceEqual(sideRows.OrderBy(card => card.Item3).ThenBy(card => card.Item1)),
                "a public retail code with sideboards decodes: hero " + hero + ", " + count + " cards, " + sideRows.Length + " sideboard cards");
            // The library's own canonical code sorts sideboard rows by owner; the client sorts them by card, as Write does.
            if (hero != 31)
                Check(Convert.FromBase64String(Write(sample.Format, sample.Hero, sample.Cards, sample.Sideboards)).SequenceEqual(Convert.FromBase64String(vector)),
                    "the writer reproduces that retail code byte for byte (hero " + hero + ")");
        }

        // Round trip of every field the client writes, including counts above two and sideboards of every count.
        var cards = new List<(int, int)> { (900, 1), (40, 2), (77, 2), (5000, 3), (123456, 1) };
        var boards = new List<(int, int, int)> { (300, 1, 900), (301, 2, 900), (302, 4, 77) };
        var back = DeckCodeText.TryDecode(Write(1, 637, cards, boards));
        Check(back != null && back.Format == 1 && back.Hero == 637 && Summed(back.Cards).SequenceEqual(Summed(cards))
            && back.Sideboards.OrderBy(card => card.dbId).SequenceEqual(boards.OrderBy(card => card.Item1)),
            "write then read gives the same deck: format, hero, 1/2/more copies, sideboards with 1/2/more copies");
        // The client lists a card once per version (one Golden, one normal copy = two single rows): the copies add up.
        var split = DeckCodeText.TryDecode(Write(2, 7, new[] { (40, 1), (40, 1), (41, 2) }));
        Check(split != null && split.CardCount == 4 && Summed(split.Cards).SequenceEqual(new[] { (40, 2), (41, 2) }), "a card listed once per version keeps both copies");

        // Not codes: nothing, damaged, truncated, other version, two heroes, a zero card.
        foreach (var (text, label) in new[] { ((string)null, "null"), ("", "empty"), ("hello world", "plain text"), ("AAEC", "truncated header"),
            ("AQECAR8AAAA=", "reserved byte not zero"), ("AAEAAR8AAAA=", "format 0"), ("AAEJAR8AAAA=", "unknown format 9"),
            ("AAECAR8" + new string('A', 2000) + "=", "too long to be a code"), ("AAICAR8AAAA=", "version 2"), ("AAECAh8fAAAA", "two heroes"), ("AAECAR8BAAAA", "a zero card id"),
            (Write(2, 31, retail.Cards).Substring(0, 20), "cut short") })
            Check(DeckCodeText.TryDecode(text) == null, "not a deck code: " + label);

        // Finding the code in pasted text.
        string code = Write(2, Db("HERO_04"), new[] { (Db("JAIL_397"), 1) });
        string found = DeckCodeText.FindCode(RetailText("Beatrix Paladin", code), out string name, out bool rescued);
        Check(found == code && name == "Beatrix Paladin" && !rescued, "retail text: the client's own rule finds the code and the name; nothing to rescue");
        string discord = RetailText("Beatrix Paladin", code).Replace("### ", "").Replace("# ", "");
        found = DeckCodeText.FindCode(discord, out name, out rescued);
        Check(found == code && name == "" && rescued, "text copied from a Discord message (heading '#' gone): the code line is found");
        found = DeckCodeText.FindCode("check this out\r\n`" + code + "`\r\n", out name, out rescued);
        Check(found == code && rescued, "a code in backticks with Windows line ends is found");
        found = DeckCodeText.FindCode("  " + code + "  ", out name, out rescued);
        Check(found == code && !rescued && name == "", "a bare code is the client's own case");
        found = DeckCodeText.FindCode("### Deck\nnot a code\n", out name, out rescued);
        Check(found == "not a code" && !rescued, "text with no code is left to the client, which refuses it");
        Check(DeckCodeText.FindCode("# only\n# comments\n", out _, out rescued) == null && !rescued, "comment-only text has no code");

        // Share round trip through the import: a Paladin deck with Beatrix and her sideboard pick, as the client writes it, reads
        // back as the same deck, nothing dropped.
        var pool = LocalDecks.CustomCards.Where(card => LocalDecks.IsCustomCard(card.Id)).ToList();
        var paladin = LocalDecks.HeroClass("HERO_04");
        var plain = pool.Where(card => card.Class == CardClass.NEUTRAL && card.Rarity != Rarity.LEGENDARY && !LocalCardCatalog.IsCore(card.Id)
            && LocalDecks.FabledBundle(card.Id).Count == 0 && !LocalDecks.IsUnsupportedSideboardOwner(card.Id) && !LocalDecks.HasModelledSideboard(card.Id)
            && LocalDecks.DeckSize(new[] { card.Id }) == 30).OrderBy(card => card.Id, StringComparer.Ordinal).Take(14).Select(card => card.Id).ToList();
        string pick = pool.Where(card => LocalDecks.SideboardEligible("JAIL_397", card.Id) && card.Id != "JAIL_397" && !plain.Contains(card.Id))
            .OrderBy(card => card.Id, StringComparer.Ordinal).First().Id;
        string legendary = pool.Where(card => card.Class == paladin && card.Rarity == Rarity.LEGENDARY && card.Id != "JAIL_397" && !LocalCardCatalog.IsCore(card.Id)
            && LocalDecks.FabledBundle(card.Id).Count == 0 && !LocalDecks.HasModelledSideboard(card.Id) && LocalDecks.DeckSize(new[] { card.Id }) == 30)
            .OrderBy(card => card.Id, StringComparer.Ordinal).First().Id;
        Check(plain.Count == 14, "fixture: fourteen plain neutral cards");
        var deck = plain.SelectMany(id => new[] { id, id }).Concat(new[] { "JAIL_397", legendary }).ToList();
        string shared = RetailText("My Paladin", Write(2, Db("HERO_04"), deck.GroupBy(id => id).Select(group => (Db(group.Key), group.Count())),
            new[] { (Db(pick), 1, Db("JAIL_397")) }));
        var read = DeckCodeText.TryDecode(DeckCodeText.FindCode(shared, out string sharedName));
        Check(read != null && read.CardCount == 30 && Id(read.Hero) == "HERO_04", "the shared text reads back: 30 cards, Paladin");
        var plan = NativeDeckCodeImport.Plan(Id(read.Hero), sharedName, "New deck",
            read.Cards.Select(card => (Id(card.dbId), card.qty)), read.Sideboards.Select(card => (Id(card.owner), Id(card.dbId), card.qty)));
        Check(plan.Dropped == 0 && plan.Name == "My Paladin" && plan.Cards.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(deck.OrderBy(id => id, StringComparer.Ordinal))
            && plan.Sideboards.Count == 1 && plan.Sideboards[0].Source == "JAIL_397" && plan.Sideboards[0].Cards.SequenceEqual(new[] { pick }),
            "copy then paste gives the same deck: cards, name, Beatrix's sideboard, nothing dropped");
        // The same text after Discord stripped its headings imports the same cards (the name falls back).
        var mangled = DeckCodeText.TryDecode(DeckCodeText.FindCode(shared.Replace("### ", "").Replace("# ", ""), out string mangledName));
        var mangledPlan = NativeDeckCodeImport.Plan(Id(mangled.Hero), mangledName, "New deck",
            mangled.Cards.Select(card => (Id(card.dbId), card.qty)), mangled.Sideboards.Select(card => (Id(card.owner), Id(card.dbId), card.qty)));
        Check(mangledPlan.Cards.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(plan.Cards.OrderBy(id => id, StringComparer.Ordinal))
            && mangledPlan.Sideboards.Single().Cards.SequenceEqual(new[] { pick }) && mangledPlan.Name == "New deck", "after Discord: same cards and sideboard, default name");
        return checks;
    }
}
