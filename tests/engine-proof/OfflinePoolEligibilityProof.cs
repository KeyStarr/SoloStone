using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;

internal static class OfflinePoolEligibilityProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "offline pool eligibility: " + message);
    private static XDocument Catalog() => XDocument.Load("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml");
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("accepted invalid catalog: " + message);
    }

    private static bool MetadataMatches(OfflinePoolPolicy.Member member)
    {
        var card = OfflinePoolManifest.Definition(member);
        var schools = new[] { "NONE", "ARCANE", "FIRE", "FROST", "NATURE", "HOLY", "SHADOW", "FEL" };
        int school = card[GameTag.SPELL_SCHOOL];
        return member.Cost == card.Cost && member.Type == card.Type.ToString() &&
            member.Attack == card.ATK && member.Health == card.Health && member.Rarity == card.Rarity.ToString() &&
            school >= 0 && school < schools.Length && member.SpellSchool == schools[school] &&
            member.Races.OrderBy(r => r).SequenceEqual(card.Races.Select(r => r.ToString()).OrderBy(r => r));
    }

    private static void AllMemberMetadata(OfflinePoolPolicy policy)
    {
        foreach (var member in policy.Members)
            Check(MetadataMatches(member), "catalog/engine cost, type, stats, school, races, rarity: " + member.CardId);
        var invalidClasses = policy.Members.Where(m => Cards.FromId(m.CardId).Class == CardClass.INVALID).ToArray();
        Check(invalidClasses.All(m => m.Classes.Count > 1),
            "invalid legacy primary classes retain explicit export multiclass membership");

        // Prove the comparison detects each field drifting, without changing the installed catalog or goldens.
        foreach (var change in new[] { ("cost", "999"), ("type", "INVALID"), ("attack", "999"),
            ("health", "999"), ("spellSchool", "UNKNOWN"), ("rarity", "UNKNOWN") })
        {
            var row = new XElement(Catalog().Root.Element("Members").Elements("Card").First());
            row.SetAttributeValue(change.Item1, change.Item2);
            Check(!MetadataMatches(new OfflinePoolPolicy.Member(row)), "detect metadata drift: " + change.Item1);
        }
        var races = new XElement(Catalog().Root.Element("Members").Elements("Card").First());
        races.Elements("Race").Remove();
        races.Add(new XElement("Race", new XAttribute("name", "UNKNOWN")));
        Check(!MetadataMatches(new OfflinePoolPolicy.Member(races)), "detect race drift");
        Console.WriteLine($"PASS offline pool metadata: all {policy.Members.Count} admitted members match engine cost/type/attack/health/school/races/rarity; Class INVALID multiclass members use catalog classes; seven drift mutations rejected");
    }

    internal static void Run()
    {
        // The admission tool owns export normalization and executed-proof/transport checks.
        var start = new ProcessStartInfo(File.Exists("/usr/bin/python3") ? "/usr/bin/python3" : "python3") {
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "-m", "unittest", "discover", "-s", "tests", "-p", "test_offline_pools.py" }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start);
        string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
        Check(child.WaitForExit(30000) && child.ExitCode == 0, "admission boundary cases: " + output + error);

        var game = new Game(new GameConfig { RandomSeed = 81, Logging = false });
        var random = (Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var before = random.CaptureState();
        var legacy = Cards.AllWild.ToArray();
        var policy = OfflinePoolPolicy.Load();
        policy.ValidateRegistration();
        AllMemberMetadata(policy);
        Check(random.CaptureState().Equals(before) && legacy.SequenceEqual(Cards.AllWild), "load/validate consumes no RNG or changes legacy pools");
        Check(policy.ActiveMetaId == "violet-hold" && policy.Sets.SequenceEqual(new[] { "EMERALD_DREAM", "THE_LOST_CITY", "TIME_TRAVEL", "CATACLYSM", "ESCAPEFROM_VIOLET_HOLD", "CORE" }), "ordered active meta sets");
        Check(policy.NewestSet == "ESCAPEFROM_VIOLET_HOLD" && policy.ExtraCards.Count == 22
            && policy.Metas.Single(m => m.Id == "black-empire").NewestSet == "BE", "meta extras/newest set");
        int eventSet = policy.Metas.First().ExtraCardSetIds["EDR_950"];
        Card EventCard(string id) => new Card(id, -100100,
            new[] { new Tag(GameTag.CARD_SET, eventSet), new Tag(GameTag.CARDTYPE, (int)CardType.MINION) },
            new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>());
        // BE_037 is absent from the pinned export; exercise that requested boundary identity with BE's real set id.
        Card BECard(string id) => new Card(id, -100101,
            new[] { new Tag(GameTag.CARD_SET, (int)Cards.FromId("BE_036").Set), new Tag(GameTag.CARDTYPE, (int)CardType.MINION) },
            new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>());
        Check(policy.AllowsCard(EventCard("EDR_950")) && policy.AllowsCard(EventCard("CATA_EVENT_110"))
            && policy.AllowsCard(EventCard("BE_EVENT_100")) && !policy.AllowsCard(EventCard("UNLISTED_EVENT")),
            "numeric EVENT identity restricts collection to active extras, independently of random-generation tags");
        foreach (string id in new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" })
            Check(policy.AllowsCard(Cards.FromId(id)) && policy.Members.Any(m => m.CardId == id && m.Scope == "Standard")
                && !policy.IsPackEligible(Cards.FromId(id)),
                "Violet Hold dated admission allowed but never packed: " + id);
        Check(!policy.AllowsCard(BECard("BE_037")) && !policy.AllowsCard(EventCard("BE_EVENT_103")),
            "unadmitted BE and later EVENT remain inactive");
        Check(!policy.IsPackEligible(EventCard("EDR_950")), "EVENT collection extras are never pack eligible");
        if (ClientExport.Oracle)
            Check(policy.Members.Where(m => m.Scope == "Past").Select(m => m.CardId).ToHashSet().SetEquals(OfflinePoolExpectations.Ids(PoolScope.Past)), "Past members exactly match eligibility/export");
        else ClientExport.Skip("Offline pool Past members against the independent derivation from the client export");
        Check(!policy.Members.Any(m => m.CardId == "CS2_182"), "unproven legacy excluded");
        Check(policy.Members.Any(m => m.Set == "CORE" && m.CardId.StartsWith("CORE_")), "normalized Core admitted");
        Check(policy.Members.Any(m => m.Classes.Count > 1), "export multiclass retained");

        var xml = Catalog();
        xml.Root.Element("Members").Elements("Card").First().SetAttributeValue("proofKey", "");
        Reject(() => OfflinePoolPolicy.Parse(xml), "registration without proof");
        xml = Catalog();
        var first = xml.Root.Element("Members").Elements("Card").First();
        first.SetAttributeValue("dbfId", int.MaxValue);
        Reject(() => OfflinePoolPolicy.Parse(xml).ValidateRegistration(), "identity mismatch");
        xml = Catalog();
        first = xml.Root.Element("Members").Elements("Card").First();
        first.SetAttributeValue("cardId", "UNREGISTERED_999");
        Reject(() => OfflinePoolPolicy.Parse(xml).ValidateRegistration(), "proof without registration");
        xml = Catalog();
        xml.Root.Element("Members").Elements("Card").First().Add(new XElement("Dependency", new XAttribute("cardId", "MISSING_999")));
        Reject(() => OfflinePoolPolicy.Parse(xml), "missing dependency");
        xml = Catalog();
        xml.Root.Element("Dependencies").Elements("Card").First().Add(new XElement("Dependency", new XAttribute("cardId", "MISSING_999")));
        Reject(() => OfflinePoolPolicy.Parse(xml), "transitive dependency");
        xml = Catalog();
        xml.Root.Element("Members").Add(new XElement(xml.Root.Element("Members").Elements("Card").First()));
        Reject(() => OfflinePoolPolicy.Parse(xml), "duplicate identity");
        xml = Catalog();
        xml.Root.Element("Members").Elements("Card").First().SetAttributeValue("set", "INVALID");
        Reject(() => OfflinePoolPolicy.Parse(xml), "unresolved export set");
        xml = Catalog();
        xml.Root.SetAttributeValue("activeMeta", "missing");
        Reject(() => OfflinePoolPolicy.Parse(xml), "unknown active meta");
        xml = Catalog();
        xml.Root.Element("Metas").Add(new XElement(xml.Root.Element("Metas").Elements("Meta").First()));
        Reject(() => OfflinePoolPolicy.Parse(xml), "duplicate meta");
        foreach (string newest in new[] { "CORE", "BE", "", "UNKNOWN" })
        {
            xml = Catalog();
            xml.Root.Element("Metas").Elements("Meta").First().SetAttributeValue("newestSet", newest);
            Reject(() => OfflinePoolPolicy.Parse(xml), "invalid newestSet: " + newest);
        }
        xml = Catalog();
        var extraMeta = xml.Root.Element("Metas").Elements("Meta").First();
        extraMeta.Add(new XElement(extraMeta.Elements("ExtraCard").First()));
        Reject(() => OfflinePoolPolicy.Parse(xml), "duplicate extra card");
        // Promote an already proven Past card: collection/random membership changes, pack membership does not.
        xml = Catalog();
        var extra = xml.Root.Element("Members").Elements("Card").First(c => (string)c.Attribute("scope") == "Past");
        string extraId = (string)extra.Attribute("cardId");
        xml.Root.Element("Metas").Elements("Meta").First().Add(new XElement("ExtraCard",
            new XAttribute("cardId", extraId), new XAttribute("set", (string)extra.Attribute("set")),
            new XAttribute("setId", (int)Cards.FromId(extraId).Set)));
        extra.SetAttributeValue("scope", "Standard");
        var withExtra = OfflinePoolPolicy.Parse(xml);
        var extraManifest = withExtra.BuildManifest();
        Check(withExtra.AllowsCard(Cards.FromId(extraId)) && extraManifest.Standard.Any(m => m.CardId == extraId),
            "admitted extra belongs to collection and Standard pool");
        Check(!withExtra.IsPackEligible(Cards.FromId(extraId)) && !extraManifest.PackEligibleSets.Contains("CORE")
            && !OfflineCardPools.Candidates(extraManifest, CardClass.MAGE,
                OfflinePoolPolicy.StandardPackFilter(Cards.FromId(extraId).Rarity, extraManifest), 0).Any(c => c.Id == extraId),
            "Standard pack excludes proven collection extras");
        Check(extraManifest.Hash != policy.BuildManifest().Hash, "meta extras participate in manifest hash");
        xml = Catalog();
        xml.Root.Element("Members").Elements("Card").First(c => (string)c.Attribute("set") == "BE").SetAttributeValue("scope", "Past");
        Reject(() => OfflinePoolPolicy.Parse(xml), "inactive cards cannot become Past");
        xml = KnownMetaProofFixture.BlackEmpireCatalog();
        var blackEmpire = OfflinePoolPolicy.Parse(xml);
        Check(blackEmpire.AllowsCard(Cards.FromId("BE_152")) && blackEmpire.AllowsCard(BECard("BE_037"))
            && blackEmpire.AllowsCard(Cards.FromId("BE_036")) && blackEmpire.IsPackEligible(Cards.FromId("BE_152"))
            && !blackEmpire.IsPackEligible(Cards.FromId("BE_036"))
            && blackEmpire.BuildManifest().Standard.Any(m => m.Set == "BE")
            && blackEmpire.BuildManifest().Hash != policy.BuildManifest().Hash, "meta switch restores BE membership and changes hash");
        var blackManifest = blackEmpire.BuildManifest();
        Check(OfflineCardPools.Candidates(blackManifest, CardClass.MAGE,
                OfflinePoolPolicy.StandardPackFilter(Rarity.COMMON, blackManifest), 0).Any(c => c.Id.StartsWith("BE_"))
            && !OfflineCardPools.Candidates(policy.BuildManifest(), CardClass.MAGE,
                OfflinePoolPolicy.StandardPackFilter(Rarity.COMMON), 0).Any(c => c.Id.StartsWith("BE_")),
            "generated packs use their game's pinned meta, including historical replays");
        Console.WriteLine("LIVE pool manifest hash: " + policy.BuildManifest().Hash);
        Console.WriteLine($"PASS offline pool eligibility: {policy.Members.Count} members; export overrides, identity, executed proof, registration, strict corpus, transitive closure, immutable catalog, no RNG/legacy mutation");
    }
}
