using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;

// The card file must load to exactly what the private XMLs load to: every field of every Card, in order.
internal static class CardFactsProof
{
    private const string DataPath = "engine/SabberStoneCore/resources/Data/";
    private const string FilePath = "facts/" + CardFacts.Meta + ".json";

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
        Identity(facts);
        Console.WriteLine($"PASS card file without a client: embedded file, no XML resource, text hashes, reader refusals, {facts.Identity.Length} Wild identity rows");
    }

    /// <summary>The comparison with the client's XML files (client tier): the card file must load to exactly what they load to.</summary>
    internal static void Run()
    {
        var xmlBase = XDocument.Load(DataPath + "CardDefs.xml");
        var xmlOverlay = ClientOverlay.Read(File.OpenRead(DataPath + "ClientOverlay.xml"));
        var facts = CardFacts.Read(File.OpenRead(FilePath), CardFacts.Meta);

        // The Wild identity rows come from the client export, not the XMLs (checked by Identity and the Wild proofs): both sides apply them.
        var expected = ClientOverlay.AddTo(CardLoader.ApplyIdentity(CardLoader.LoadDefinitions(xmlBase), facts.Identity), xmlOverlay);
        var actual = ClientOverlay.AddTo(CardLoader.ApplyIdentity(facts.Cards.Select(e => CardLoader.Build(e, false)).ToArray(), facts.Identity), facts.Overlay);
        Check(expected.Length == 8333 + 2381 && actual.Length == expected.Length, "card count " + actual.Length);
        var differences = new List<string>();
        for (int i = 0; i < expected.Length; i++) Compare(expected[i], actual[i], differences);
        CheckEmpty(differences, "base and overlay cards");

        CompareOverlay(xmlOverlay, facts.Overlay, differences);
        CheckEmpty(differences, "overlay rows");
        ComparatorSeesEveryDifference(xmlOverlay);
        Console.WriteLine($"PASS card file: {actual.Length} cards and {facts.Overlay.Cards.Length} overlay rows equal the XML path, {CardFields.Length} fields each");
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
        int hashed = 0, identity = 0;
        var rows = root.GetProperty("cards").EnumerateArray().Concat(root.GetProperty("overlay").GetProperty("cards").EnumerateArray());
        if (root.TryGetProperty("identity", out var identityRows))
        {
            identity = identityRows.GetArrayLength();
            rows = rows.Concat(identityRows.EnumerateArray());
        }
        foreach (var row in rows)
        {
            bool hasText = row.TryGetProperty("text", out var text), hasHash = row.TryGetProperty("textHash", out var hash);
            Check(hasText == hasHash, "text hash presence: " + row.GetProperty("id").GetString());
            if (!hasText) continue;
            string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.GetString()))).Substring(0, 16).ToLowerInvariant();
            Check(hash.GetString() == actual, "text hash: " + row.GetProperty("id").GetString());
            hashed++;
        }
        Check(hashed == 6914 + 2389 + identity, "text hash count " + hashed);
    }

    // Wild identity rows: exactly the Wild manifests' ids, each the base card's id and dbf, loaded in the base card's place with
    // the overlay rules; every other card loads from its base row. The loader refuses a row it cannot place.
    // Tags the client stores as a flag (1) and the engine reads as the amount (tools/gen_card_facts.py AMOUNT_TAGS).
    internal static readonly SabberStoneCore.Enums.GameTag[] AmountTags = { SabberStoneCore.Enums.GameTag.SPELLPOWER, SabberStoneCore.Enums.GameTag.OVERLOAD };

    internal static int? TextAmount(SabberStoneCore.Enums.GameTag tag, string text)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]*>", "");
        var found = System.Text.RegularExpressions.Regex.Match(plain, tag == SabberStoneCore.Enums.GameTag.SPELLPOWER
            ? @"Spell Damage \+(\d+)" : @"Overload:?\s*\((\d+)\)");
        return found.Success ? int.Parse(found.Groups[1].Value) : (int?)null;
    }

    // An identity card with an amount tag plays the amount its text states (Spell Damage +5 is 5, not the client's flag 1).
    private static int CheckAmounts(IEnumerable<(CardFacts.Entity Row, Card Card)> cards)
    {
        int checkedTags = 0;
        foreach (var (row, card) in cards)
            foreach (var tag in AmountTags)
            {
                if (!card.Tags.ContainsKey(tag)) continue;
                string text = (string)row.Tags.First(t => t.GameTag == SabberStoneCore.Enums.GameTag.CARDTEXT).TagValue;
                Check(card.Tags[tag] == TextAmount(tag, text), "identity amount " + tag + " of " + row.Id + ": " + card.Tags[tag]);
                Check(tag != SabberStoneCore.Enums.GameTag.SPELLPOWER || card.SpellPower == card.Tags[tag], "identity SpellPower of " + row.Id);
                Check(tag != SabberStoneCore.Enums.GameTag.OVERLOAD || card.Overload == card.Tags[tag], "identity Overload of " + row.Id);
                checkedTags++;
            }
        return checkedTags;
    }

    /// <summary>A scratch card file's identity rows (e.g. a temporary manifest entry): applied to its base rows, amounts checked.</summary>
    internal static void IdentityFile(string path)
    {
        var facts = CardFacts.Read(File.OpenRead(path), CardFacts.Meta);
        var built = facts.Cards.Select(e => CardLoader.Build(e, false)).ToArray();
        var applied = CardLoader.ApplyIdentity(built, facts.Identity);
        var at = built.Select((c, i) => (c.Id, i)).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().i);
        int amounts = CheckAmounts(facts.Identity.Select(row => (row, applied[at[row.Id]])));
        if (at.TryGetValue("EX1_563", out int malygos) && facts.Identity.Any(row => row.Id == "EX1_563"))
            Check(applied[malygos].SpellPower == 5 && applied[malygos][SabberStoneCore.Enums.GameTag.SPELLPOWER] == 5, "EX1_563 identity keeps Spell Damage +5");
        Console.WriteLine($"PASS identity file {path}: {facts.Identity.Length} rows, {amounts} amount tags"
            + (facts.Identity.Any(row => row.Id == "EX1_563") ? "; EX1_563 SpellPower=" + applied[malygos].SpellPower : ""));
    }

    private static void Identity(CardFacts.Document facts)
    {
        var listed = Directory.GetFiles("tests/progression/batches/wild", "*.txt").OrderBy(p => p, StringComparer.Ordinal)
            .SelectMany(p => File.ReadAllText(p).TrimEnd('\r', '\n').Split('\n')).Select(l => l.TrimEnd('\r')).ToArray();
        Check(facts.Identity.Select(e => e.Id).OrderBy(i => i, StringComparer.Ordinal)
            .SequenceEqual(listed.OrderBy(i => i, StringComparer.Ordinal)), "identity rows are the Wild manifests' ids");
        var order = facts.Cards.Select((e, i) => (e.Id, i)).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().i);
        Check(facts.Identity.Select(e => order[e.Id]).SequenceEqual(facts.Identity.Select(e => order[e.Id]).OrderBy(i => i)), "identity rows in base order");
        var built = facts.Cards.Select(e => CardLoader.Build(e, false)).ToArray();
        var applied = CardLoader.ApplyIdentity(built, facts.Identity);
        var rowOf = facts.Identity.ToDictionary(e => e.Id);
        var differences = new List<string>();
        for (int i = 0; i < built.Length; i++)
        {
            if (rowOf.TryGetValue(built[i].Id, out var row))
            {
                Check(built[i].AssetId == row.AssetId, "identity keeps dbf " + row.Id);
                Compare(CardLoader.Build(row, true), applied[i], differences);
                // The engine's card list carries the identity too (Cards adds scripts, not definitions).
                var engine = Cards.FromId(row.Id);
                // The Cleave rule (Lake Thresher, Magnataur Alpha, Foe Reaper 4000) is a script-owned engine tag, not part of the client row.
                Check(engine.Tags.Where(t => t.Key != SabberStoneCore.Actions.CombatRules.Cleave).OrderBy(t => (int)t.Key)
                    .SequenceEqual(applied[i].Tags.Where(t => t.Key != SabberStoneCore.Actions.CombatRules.Cleave).OrderBy(t => (int)t.Key)) && engine.Text == applied[i].Text
                    && engine.Races.SequenceEqual(applied[i].Races), "the engine loads the identity of " + row.Id);
            }
            else if (!ReferenceEquals(built[i], applied[i])) differences.Add(built[i].Id + " replaced without an identity row");
        }
        CheckEmpty(differences, "identity rows");
        CheckAmounts(facts.Identity.Select(row => (row, Cards.FromId(row.Id))));
        Check(TextAmount(SabberStoneCore.Enums.GameTag.SPELLPOWER, "<b>Spell Damage +5</b>") == 5
            && TextAmount(SabberStoneCore.Enums.GameTag.OVERLOAD, "<b><b>Overload</b>:</b> (3)") == 3
            && TextAmount(SabberStoneCore.Enums.GameTag.OVERLOAD, "Draw a card.") == null, "amount text parsing");
        // Legacy Standard pools and legality (2021 StandardSets) read an identity card by its base row's set.
        var baseById = facts.Cards.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var row in facts.Identity)
        {
            var baseSet = (SabberStoneCore.Enums.CardSet)(int)baseById[row.Id].Tags.First(t => t.GameTag == SabberStoneCore.Enums.GameTag.CARD_SET).TagValue;
            var card = Cards.FromId(row.Id);
            bool legacyStandard = Cards.StandardSets.Contains(baseSet);
            Check(Cards.LegacySet(card) == baseSet, "legacy set of " + row.Id);
            foreach (var cls in new[] { SabberStoneCore.Enums.CardClass.DEMONHUNTER, SabberStoneCore.Enums.CardClass.DEATHKNIGHT })
                Check(!Cards.Standard[cls].Contains(card) || legacyStandard, "identity card joins the legacy Standard pool of " + cls + ": " + row.Id);
            Check(Cards.IsFormatLegal(card, SabberStoneCore.Enums.FormatType.FT_STANDARD) == (legacyStandard && card.Collectible && card.Implemented),
                "legacy Standard legality of " + row.Id);
        }
        if (facts.Identity.Length == 0) return;
        var sample = facts.Identity[0];
        void Refused(string what, CardFacts.Entity entity)
        {
            bool refused = false;
            try { CardLoader.ApplyIdentity(built, new[] { entity }); }
            catch (InvalidDataException) { refused = true; }
            Check(refused, "identity refuses " + what);
        }
        Refused("an unknown id", new CardFacts.Entity { Id = sample.Id + "x", AssetId = sample.AssetId, Tags = sample.Tags });
        Refused("another dbf", new CardFacts.Entity { Id = sample.Id, AssetId = sample.AssetId + 1, Tags = sample.Tags });
        Refused("a row without set", new CardFacts.Entity { Id = sample.Id, AssetId = sample.AssetId,
            Tags = sample.Tags.Where(t => t.GameTag != SabberStoneCore.Enums.GameTag.CARD_SET).ToArray() });
        Tag[] Replaced(SabberStoneCore.Enums.GameTag tag, int value) =>
            sample.Tags.Select(t => t.GameTag == tag ? new Tag(tag, value) : t).ToArray();
        int type = (int)sample.Tags.First(t => t.GameTag == SabberStoneCore.Enums.GameTag.CARDTYPE).TagValue;
        Refused("a card type change", new CardFacts.Entity { Id = sample.Id, AssetId = sample.AssetId,
            Tags = Replaced(SabberStoneCore.Enums.GameTag.CARDTYPE, type == 4 ? 7 : 4) });
        Refused("a non-collectible row", new CardFacts.Entity { Id = sample.Id, AssetId = sample.AssetId,
            Tags = Replaced(SabberStoneCore.Enums.GameTag.COLLECTIBLE, 0) });
        bool duplicate = false;
        try { CardLoader.ApplyIdentity(built, new[] { sample, sample }); } catch (InvalidDataException) { duplicate = true; }
        Check(duplicate, "identity refuses a duplicate row");
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
        string WithIdentity(string row) => File("").Substring(0, File("").Length - 1) + ",\"identity\":[" + row + "]}";
        Check(Parse(WithIdentity("{\"id\":\"A\",\"dbf\":1,\"name\":\"n\",\"text\":\"t\",\"tags\":[[1,2]]}")).Identity.Single().Tags.Length == 3, "identity rows read");
        Check(Parse(File("")).Identity.Length == 0, "a file without identity rows has none");
        Refused("identity row without text", WithIdentity("{\"id\":\"A\",\"dbf\":1,\"name\":\"n\",\"tags\":[]}"));
        Refused("overlay field on an identity row", WithIdentity("{\"id\":\"A\",\"dbf\":1,\"name\":\"n\",\"text\":\"t\",\"tags\":[],\"originalId\":\"B\"}"));
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
