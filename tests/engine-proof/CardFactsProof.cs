using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;

// The card file must load to exactly what the private XMLs load to: every field of every Card, in order. The base rows listed in
// `clientRows` of the drift file (facts/drift/<meta>.json) are the exception: they load to what the client export gives (see ClientRowsFromExport).
internal static class CardFactsProof
{
    private const string DataPath = "engine/SabberStoneCore/resources/Data/";
    private const string FilePath = "facts/" + CardFacts.Meta + ".json";
    private const string DriftPath = "facts/drift/" + CardFacts.Meta + ".json";

    // Reflection, so a field added to Card later is compared without touching this proof.
    private static readonly FieldInfo[] CardFields = typeof(Card)
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    /// <summary>The checks that need no client file: the engine embeds the card file and neither XML, the embedded copy is the
    /// file on disk, the file's text hashes hold, and the reader refuses bad input. Run by the engine-proof suite.</summary>
    internal static void RunWithoutClient()
    {
        Check(CardFields.Length > 40, "Card field reflection found the backing fields");
        var facts = CardFacts.Read(File.OpenRead(FilePath), CardFacts.Meta);
        Check(facts.Overlay.Cards.Count(d => d.ScopedSnapshot) == 8, "scoped snapshots are in the file");

        // The embedded copy is the file on disk, and the engine loads what the client half of this proof compares.
        Check(CardFacts.Embedded.Meta == CardFacts.Meta, "embedded meta label");
        Check(typeof(Card).Assembly.GetManifestResourceNames().Any(n => n.EndsWith(CardFacts.Meta + ".json", StringComparison.Ordinal))
            && !typeof(Card).Assembly.GetManifestResourceNames().Any(n => n.Contains("CardDefs") || n.Contains("ClientOverlay")),
            "the engine embeds the card file and neither XML");
        using (var embedded = typeof(Card).Assembly.GetManifestResourceStream("SabberStoneCore.Resources.Facts." + CardFacts.Meta + ".json"))
        using (var bytes = new MemoryStream())
        {
            embedded.CopyTo(bytes);
            Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes(FilePath)), "embedded file equals " + FilePath);
        }
        TextHashes();
        ReaderRefusesBadInput();
        Console.WriteLine("PASS card file without a client: embedded file, no XML resource, text hashes, reader refusals");
    }

    /// <summary>The comparison with the client's XML files (client tier): the card file must load to exactly what they load to.</summary>
    internal static void Run()
    {
        var xmlBase = XDocument.Load(DataPath + "CardDefs.xml");
        var xmlOverlay = ClientOverlay.Read(File.OpenRead(DataPath + "ClientOverlay.xml"));
        var facts = CardFacts.Read(File.OpenRead(FilePath), CardFacts.Meta);

        var baseCards = CardLoader.LoadDefinitions(xmlBase);
        AddSpellSchools(baseCards);
        int fromExport = ClientRowsFromExport(baseCards, facts);
        var expected = ClientOverlay.AddTo(baseCards, xmlOverlay);
        var actual = ClientOverlay.AddTo(facts.Cards.Select(e => CardLoader.Build(e, false)).ToArray(), facts.Overlay);
        Check(expected.Length == 8333 + 2381 && actual.Length == expected.Length, "card count " + actual.Length);
        var differences = new List<string>();
        for (int i = 0; i < expected.Length; i++) Compare(expected[i], actual[i], differences);
        CheckEmpty(differences, "base and overlay cards");

        CompareOverlay(xmlOverlay, facts.Overlay, differences);
        CheckEmpty(differences, "overlay rows");
        ComparatorSeesEveryDifference(xmlOverlay);
        Console.WriteLine($"PASS card file: {actual.Length} cards and {facts.Overlay.Cards.Length} overlay rows equal the XML path ({fromExport} base rows the export path), {CardFields.Length} fields each");
    }

    /// <summary>The generator's one input besides the XMLs (private/tools/gen_card_facts.py): the base rows come from a card build that
    /// predates spell schools, so a base row without tag 1635 takes the client export's school. Nothing else is taken from the export.</summary>
    private static void AddSpellSchools(Card[] baseCards)
    {
        var export = ClientExport.RequireCards();
        int added = 0;
        foreach (var card in baseCards)
        {
            if (card.Tags.ContainsKey(GameTag.SPELL_SCHOOL) || !export.TryGetValue(card.Id, out var row)
                || !row.GetProperty("tags").TryGetProperty("1635", out var school)) continue;
            var tags = card.Tags.Concat(new[] { new KeyValuePair<GameTag, int>(GameTag.SPELL_SCHOOL, school.GetInt32()) }).OrderBy(pair => (int)pair.Key).ToList();
            card.Tags.Clear();
            foreach (var pair in tags) card.Tags[pair.Key] = pair.Value;
            added++;
        }
        Check(added == 579, "the export adds a spell school to 579 base rows: " + added);
    }

    /// <summary>The base rows named in the drift file's clientRows are built from the client export, not from the XML (the generator's
    /// rule, private/tools/card_drift.py build_client_row): the export's integer tags without zeros and without CARD_SET, a weapon's
    /// tag 45 as DURABILITY when the export has no 187, plus the XML row's own CARD_SET, sorted by tag number; name and text when non-empty;
    /// referenced tags without zeros, sorted. The expected card for each such id is rebuilt here from the export, independently of the
    /// generator, and the XML card is replaced by it. Returns how many.</summary>
    private static int ClientRowsFromExport(Card[] baseCards, CardFacts.Document facts)
    {
        using var drift = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(DriftPath));
        var root = drift.RootElement;
        Check(root.GetProperty("meta").GetString() == CardFacts.Meta && root.GetProperty("exportSha256").GetString() == facts.Overlay.ExportSha256,
            "the drift file belongs to this card file");
        var ids = root.GetProperty("clientRows").EnumerateArray().Select(e => e.GetString()).ToList();
        var export = ClientExport.RequireCards();
        var overlayIds = new HashSet<string>(facts.Overlay.Cards.Select(d => d.CardId));
        foreach (string id in ids)
        {
            Check(!overlayIds.Contains(id), "a client row is a base row: " + id);
            int at = Array.FindIndex(baseCards, c => c.Id == id);
            Check(at >= 0, "a client row is in the XML: " + id);
            Check(export.TryGetValue(id, out var row), "a client row is in the export: " + id);
            Card old = baseCards[at];
            Check(row.GetProperty("dbfId").GetInt32() == old.AssetId, "client row dbf equals the XML's: " + id);
            var tags = new SortedDictionary<int, int>();
            foreach (var pair in row.GetProperty("tags").EnumerateObject())
                if (pair.Value.GetInt32() != 0 && pair.Name != ((int)GameTag.CARD_SET).ToString()) tags[int.Parse(pair.Name)] = pair.Value.GetInt32();
            if (row.GetProperty("cardType").GetString() == "WEAPON" && tags.ContainsKey((int)GameTag.HEALTH) && !tags.ContainsKey((int)GameTag.DURABILITY))
            { tags[(int)GameTag.DURABILITY] = tags[(int)GameTag.HEALTH]; tags.Remove((int)GameTag.HEALTH); }
            Check(tags.TryGetValue((int)GameTag.CARDTYPE, out int type) && old.Tags.TryGetValue(GameTag.CARDTYPE, out int oldType) && type == oldType, "client row type equals the XML's: " + id);
            if (old.Tags.TryGetValue(GameTag.CARD_SET, out int set)) tags[(int)GameTag.CARD_SET] = set;
            var all = tags.Select(pair => new Tag((GameTag)pair.Key, pair.Value)).ToList();
            string name = row.GetProperty("name").GetString(), text = row.GetProperty("text").GetString();
            if (!string.IsNullOrEmpty(name)) all.Add(new Tag(GameTag.CARDNAME, name));
            if (!string.IsNullOrEmpty(text)) all.Add(new Tag(GameTag.CARDTEXT, text));
            var refs = row.GetProperty("referencedTags").EnumerateObject().Where(p => p.Value.GetInt32() != 0)
                .Select(p => new Tag((GameTag)int.Parse(p.Name), p.Value.GetInt32())).OrderBy(t => (int)t.GameTag).ToArray();
            var entity = new CardFacts.Entity { Id = id, AssetId = old.AssetId, Tags = all.ToArray(), ReferencedTags = refs.Length > 0 ? refs : null,
                Requirements = old.PlayRequirements?.ToDictionary(pair => pair.Key, pair => pair.Value), Entourage = old.Entourage?.ToArray() };
            baseCards[at] = CardLoader.Build(entity, false);
        }
        return ids.Count;
    }

    private static void Compare(Card expected, Card actual, List<string> differences)
    {
        foreach (FieldInfo field in CardFields)
        {
            string left = Render(field.GetValue(expected)), right = Render(field.GetValue(actual));
            if (left != right)
                differences.Add($"{expected.Id}/{actual.Id} {field.Name}: {left} != {right}");
        }
    }

    // Dictionaries render in iteration order: Tags order reaches entity creation and so the recorded games.
    private static string Render(object value)
    {
        switch (value)
        {
            case null: return "<null>";
            case string text: return "\"" + text + "\"";
            case Delegate call: return "delegate:" + string.Join("+", call.GetInvocationList().Select(d => d.Method.DeclaringType + "." + d.Method.Name));
            case IDictionary map:
                var pairs = new List<string>();
                foreach (DictionaryEntry pair in map) pairs.Add(Convert.ToInt32(pair.Key) + "=" + Render(pair.Value));
                return "{" + string.Join(",", pairs) + "}";
            case IEnumerable items:
                return "[" + string.Join(",", items.Cast<object>().Select(Render)) + "]";
            default:
                return value.GetType().IsEnum ? value.GetType().Name + ":" + Convert.ToInt32(value)
                    : value.GetType().IsPrimitive ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException("Unhandled Card field value: " + value.GetType());
        }
    }

    private static void CompareOverlay(ClientOverlay.Document expected, ClientOverlay.Document actual, List<string> differences)
    {
        void Same(string what, object left, object right)
        {
            if (Render(left) != Render(right)) differences.Add($"{what}: {Render(left)} != {Render(right)}");
        }
        Same("schema", expected.SchemaVersion, actual.SchemaVersion);
        Same("build", expected.SourceBuild, actual.SourceBuild);
        Same("export", expected.ExportSha256, actual.ExportSha256);
        Same("rows", expected.Cards.Length, actual.Cards.Length);
        for (int i = 0; i < expected.Cards.Length; i++)
        {
            ClientOverlay.Definition left = expected.Cards[i], right = actual.Cards[i];
            Same(left.CardId + " id", left.CardId, right.CardId);
            Same(left.CardId + " dbf", left.DbfId, right.DbfId);
            Same(left.CardId + " name", left.Name, right.Name);
            Same(left.CardId + " text", left.Text, right.Text);
            Same(left.CardId + " snapshot", left.ScopedSnapshot, right.ScopedSnapshot);
            Same(left.CardId + " original", left.OriginalId, right.OriginalId);
            Same(left.CardId + " originalDbf", left.OriginalDbfId, right.OriginalDbfId);
            Same(left.CardId + " tags", string.Join(",", left.Tags), string.Join(",", right.Tags));
            Same(left.CardId + " refs", string.Join(",", left.ReferencedTags), string.Join(",", right.ReferencedTags));
            // Includes the 8 scoped snapshots, which AddTo skips and modules build through Snapshot.
            Compare(ClientOverlay.CreateCard(left), ClientOverlay.CreateCard(right), differences);
        }
    }

    private static void TextHashes()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(FilePath));
        var root = document.RootElement;
        Check(root.GetProperty("meta").GetString() == CardFacts.Meta, "file meta label");
        int hashed = 0;
        foreach (var row in root.GetProperty("cards").EnumerateArray().Concat(root.GetProperty("overlay").GetProperty("cards").EnumerateArray()))
        {
            bool hasText = row.TryGetProperty("text", out var text), hasHash = row.TryGetProperty("textHash", out var hash);
            Check(hasText == hasHash, "text hash presence: " + row.GetProperty("id").GetString());
            if (!hasText) continue;
            string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.GetString()))).Substring(0, 16).ToLowerInvariant();
            Check(hash.GetString() == actual, "text hash: " + row.GetProperty("id").GetString());
            hashed++;
        }
        Check(hashed == 6914 + 2389, "text hash count " + hashed);
    }

    // Guard against a comparison that cannot fail: each mutation must be seen.
    private static void ComparatorSeesEveryDifference(ClientOverlay.Document overlay)
    {
        var original = overlay.Cards.First(d => !d.ScopedSnapshot && d.Entity.Tags.Length > 4).Entity;
        Card baseline = CardLoader.Build(original, true);
        void Mutated(string what, Func<CardFacts.Entity, CardFacts.Entity> change)
        {
            var seen = new List<string>();
            Compare(baseline, CardLoader.Build(change(Copy(original)), true), seen);
            Check(seen.Count > 0, "comparator sees " + what);
        }
        int first = Array.FindIndex(original.Tags, t => t.TagValue.HasIntValue);
        Mutated("a value", e => { e.Tags[first] = new Tag(e.Tags[first].GameTag, (int)e.Tags[first].TagValue + 1); return e; });
        Mutated("tag order", e => { Array.Reverse(e.Tags); return e; });
        Mutated("a missing tag", e => { e.Tags = e.Tags.Skip(1).ToArray(); return e; });
        Mutated("text", e => { e.Tags = e.Tags.Append(new Tag(SabberStoneCore.Enums.GameTag.CARDTEXT, "changed")).ToArray(); return e; });
        Mutated("identity", e => { e.Id += "x"; return e; });
        Mutated("a reference", e => { e.ReferencedTags = new[] { new Tag((SabberStoneCore.Enums.GameTag)1, 1) }; return e; });
        Mutated("a requirement", e => { e.Requirements = new() { [SabberStoneCore.Enums.PlayReq.REQ_TARGET_TO_PLAY] = 0 }; return e; });
        Mutated("entourage", e => { e.Entourage = new[] { "x" }; return e; });
    }

    private static CardFacts.Entity Copy(CardFacts.Entity e) => new CardFacts.Entity {
        Id = e.Id, AssetId = e.AssetId, Tags = (Tag[])e.Tags.Clone(), Requirements = e.Requirements,
        Entourage = e.Entourage, ReferencedTags = e.ReferencedTags };

    private static void ReaderRefusesBadInput()
    {
        CardFacts.Document Parse(string json, string meta = "m") => CardFacts.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), meta);
        const string overlay = "\"overlay\":{\"schemaVersion\":1,\"sourceBuild\":\"b\",\"exportSha256\":\"e\",\"cards\":[]}";
        string File(string cards) => "{\"schemaVersion\":1,\"meta\":\"m\",\"cards\":[" + cards + "]," + overlay + "}";

        // Escapes and absent-versus-empty text, the two things a hand reader gets wrong first.
        var row = Parse(File("{\"id\":\"A\\u0041\\n\\\"\\\\\\/\",\"dbf\":-7,\"name\":\"\",\"text\":\"\\ud83d\\ude00\\t\",\"tags\":[[1,2],[1,-2]]}")).Cards.Single();
        Check(row.Id == "AA\n\"\\/" && row.AssetId == -7 && row.Tags.Length == 4, "escapes, negatives and duplicate tags survive");
        Check((string)row.Tags[2].TagValue == "" && (string)row.Tags[3].TagValue == "\U0001F600\t", "strings decode");
        Check(Parse(File("{\"id\":\"A\",\"dbf\":1,\"tags\":[]}")).Cards.Single().Tags.Length == 0, "no name or text adds no string tags");
        Check(CardLoader.Build(Parse(File("{\"id\":\"A\",\"dbf\":1,\"text\":\"\",\"tags\":[]}")).Cards.Single(), false).Text == "", "empty text stays empty");
        Check(CardLoader.Build(Parse(File("{\"id\":\"A\",\"dbf\":1,\"tags\":[]}")).Cards.Single(), false).Text == null, "absent text stays null");

        // Neither XML has requirements or entourage today; a later meta may. Both readers must agree on them.
        int target = (int)SabberStoneCore.Enums.PlayReq.REQ_TARGET_TO_PLAY, maxAttack = (int)SabberStoneCore.Enums.PlayReq.REQ_TARGET_MAX_ATTACK;
        var xmlRow = new XDocument(new XElement("CardDefs", new XElement("Entity", new XAttribute("CardID", "A"), new XAttribute("ID", 1),
            new XElement("PlayRequirement", new XAttribute("reqID", maxAttack), new XAttribute("param", "3")),
            new XElement("PlayRequirement", new XAttribute("reqID", target), new XAttribute("param", "")),
            new XElement("EntourageCard", new XAttribute("cardID", "B")), new XElement("EntourageCard", new XAttribute("cardID", "C")),
            new XElement("ReferencedTag", new XAttribute("enumID", 5), new XAttribute("value", 1)),
            new XElement("ReferencedTag", new XAttribute("enumID", 5), new XAttribute("value", 2)))));
        var jsonRow = Parse(File("{\"id\":\"A\",\"dbf\":1,\"tags\":[],\"refs\":[[5,1],[5,2]],\"reqs\":[[" + maxAttack + ",3],[" + target + ",0]],\"entourage\":[\"B\",\"C\"]}")).Cards.Single();
        Card fromXml = CardLoader.LoadDefinitions(xmlRow).Single(), fromJson = CardLoader.Build(jsonRow, false);
        var rowDifferences = new List<string>();
        Compare(fromXml, fromJson, rowDifferences);
        CheckEmpty(rowDifferences, "requirement and entourage row");
        Check(fromJson.PlayRequirements.Keys.First() == SabberStoneCore.Enums.PlayReq.REQ_TARGET_MAX_ATTACK && fromJson.PlayRequirements.Count == 2
            && fromJson.PlayRequirements[SabberStoneCore.Enums.PlayReq.REQ_TARGET_MAX_ATTACK] == 3 && fromJson.MustHaveTargetToPlay
            && fromJson.Entourage.SequenceEqual(new[] { "B", "C" }) && fromJson.RefTags[(SabberStoneCore.Enums.GameTag)5] == 1, "requirements, entourage and references load");
        Check(!ReferenceEquals(fromJson.PlayRequirements, CardLoader.Build(jsonRow, false).PlayRequirements), "each built card owns its requirements");

        void Refused(string what, string json, string meta = "m")
        {
            bool refused = false;
            try { Parse(json, meta); }
            catch (InvalidDataException) { refused = true; }
            Check(refused, "reader refuses " + what);
        }
        Refused("wrong meta label", File(""), "other");
        Refused("truncated file", File("").Substring(0, 20));
        Refused("trailing data", File("") + "x");
        Refused("unknown key", File("{\"id\":\"A\",\"dbf\":1,\"tags\":[],\"flavor\":\"x\"}"));
        Refused("missing dbf", File("{\"id\":\"A\",\"tags\":[]}"));
        Refused("missing tags", File("{\"id\":\"A\",\"dbf\":1}"));
        Refused("out of range integer", File("{\"id\":\"A\",\"dbf\":2147483648,\"tags\":[]}"));
        Refused("overlay field on a base row", File("{\"id\":\"A\",\"dbf\":1,\"tags\":[],\"snapshot\":true}"));
        Refused("bad escape", File("{\"id\":\"\\q\",\"dbf\":1,\"tags\":[]}"));
        Refused("unterminated string", "{\"meta\":\"m");
        Refused("schema version", File("").Replace("\"schemaVersion\":1,\"meta\"", "\"schemaVersion\":2,\"meta\""));
        Refused("overlay row without text", File("").Replace("\"cards\":[]}", "\"cards\":[{\"id\":\"A\",\"dbf\":1,\"name\":\"n\",\"tags\":[]}]}"));
    }

    private static void CheckEmpty(List<string> differences, string what)
    {
        if (differences.Count > 0)
            throw new InvalidOperationException($"{what}: {differences.Count} differences, first: " + string.Join("\n", differences.Take(20)));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("CardFactsProof: " + message);
    }
}
