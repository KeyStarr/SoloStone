using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class OfflinePoolFixtures
{
    internal static void Check(bool value, string message) => Core2026Proof.Check(value, "offline pools: " + message);
    internal static XDocument Catalog() => XDocument.Load("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml");
    internal static XDocument FixtureCatalog(IEnumerable<string> required)
    {
        var xml = Catalog();
        var rows = xml.Root.Element("Members");
        var missing = required.Except(rows.Elements("Card").Select(c => (string)c.Attribute("cardId"))).ToArray();
        if (missing.Length == 0) return xml;
        var pinned = PinnedPoolFixture.Catalog();
        foreach (string id in missing)
            rows.Add(new XElement(pinned.Root.Element("Members").Elements("Card").Single(c => (string)c.Attribute("cardId") == id)));
        var dependencies = xml.Root.Element("Dependencies");
        var existing = dependencies.Elements("Card").Select(c => (string)c.Attribute("cardId")).ToHashSet();
        foreach (var row in pinned.Root.Element("Dependencies").Elements("Card").Where(c => !existing.Contains((string)c.Attribute("cardId"))))
            dependencies.Add(new XElement(row));
        return xml;
    }
    internal static XDocument M0Catalog()
    {
        var xml = PinnedPoolFixture.Catalog();
        var pilots = SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Pilots.Select(c => c.CardId).ToHashSet();
        xml.Root.Element("Members").Elements("Card").Where(c => pilots.Contains((string)c.Attribute("cardId"))
            || !OfflinePoolM1Fixture.Members.Contains((string)c.Attribute("cardId"))).Remove();
        return xml;
    }
    internal static OfflinePoolManifest Manifest(XDocument xml) => OfflinePoolPolicy.Parse(xml).BuildManifest();

    // Registered identities, deliberately different metadata: filters must read the catalog, not Card.
    internal static XDocument Fixture(int count = 6)
    {
        var xml = PinnedPoolFixture.Catalog();
        var section = xml.Root.Element("Members");
        var standard = section.Elements("Card").Where(c => (string)c.Attribute("scope") == "Standard").Take(6).ToArray();
        var past = section.Elements("Card").Where(c => (string)c.Attribute("scope") == "Past" && LegacyPoolEligibilityProof.Ids.Contains((string)c.Attribute("cardId"))).ToArray();
        section.RemoveNodes();
        string[] classes = { "NEUTRAL", "MAGE", "DRUID,PRIEST", "DEMONHUNTER", "DRUID", "PALADIN" };
        string[] races = { "BEAST", "DRAGON,BEAST", "", "", "ALL", "" };
        string[] schools = { "NONE", "NONE", "NATURE", "SHADOW", "NONE", "FROST" };
        string[] rarities = { "COMMON", "RARE", "EPIC", "LEGENDARY", "RARE", "COMMON" };
        int[] costs = { 1, 3, 1, 4, 4, 5 }, tags = { 0, 1, -1, 2, 0, 1 };
        for (int i = 0; i < count; i++)
        {
            var card = standard[i];
            card.RemoveNodes();
            card.SetAttributeValue("type", i == 0 || i == 1 || i == 4 ? "MINION" : "SPELL");
            card.SetAttributeValue("cost", costs[i]); card.SetAttributeValue("spellSchool", schools[i]);
            card.SetAttributeValue("rarity", rarities[i]);
            foreach (var name in classes[i].Split(',')) card.Add(new XElement("Class", new XAttribute("name", name)));
            foreach (var name in races[i].Split(',', StringSplitOptions.RemoveEmptyEntries))
                card.Add(new XElement("Race", new XAttribute("name", name)));
            if (i != 4) card.Add(new XElement("Tag", new XAttribute("id", (int)GameTag.TAUNT), new XAttribute("value", tags[i])));
            section.Add(card);
        }
        section.Add(past);
        xml.Root.Element("Dependencies").RemoveNodes();
        return xml;
    }
}

internal static class OfflinePoolFilterProof
{
    internal static void Run()
    {
        ColossalDestinationRegressionProof.Run();
        ColossalDestinations();
        var template = new OfflinePoolFilter(PoolScope.Past, PoolClassMode.Exact, CardClass.PRIEST,
            CardType.SPELL, 1, 9, Race.ALL, true, 4, Rarity.RARE, GameTag.TAUNT);
        var maximum = template.WithMaxCost(3);
        OfflinePoolFixtures.Check(maximum == new OfflinePoolFilter(PoolScope.Past, PoolClassMode.Exact, CardClass.PRIEST,
            CardType.SPELL, 1, 3, Race.ALL, true, 4, Rarity.RARE, GameTag.TAUNT), "WithMaxCost preserves every other field");
        var manifest = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture());
        var ids = manifest.Standard.Select(m => m.DbfId).ToArray();
        void Expect(OfflinePoolFilter filter, int[] indices, CardClass hero = CardClass.DRUID, int source = 0)
        {
            var result = OfflineCardPools.Candidates(manifest, hero, filter, source);
            OfflinePoolFixtures.Check(result.Select(c => c.AssetId).SequenceEqual(indices.Select(i => ids[i])), "filter result " + string.Join(",", indices));
        }
        Expect(OfflinePoolFilter.Random(), new[] { 0, 1, 2, 3, 4, 5 });
        Expect(OfflinePoolFilter.Discover(), new[] { 0, 2, 4 });
        Expect(OfflinePoolFilter.Discover(), new[] { 0, 3 }, (CardClass)14);
        Expect(new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID), new[] { 2, 4 });
        Expect(new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.PRIEST), new[] { 2 });
        Expect(new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.PALADIN), new[] { 5 });
        Expect(new OfflinePoolFilter(classMode: PoolClassMode.OtherClass), new[] { 1, 3, 5 });
        Expect(new OfflinePoolFilter(classMode: PoolClassMode.OtherClass), new[] { 1, 3, 4, 5 }, CardClass.PRIEST);
        Expect(OfflinePoolFilter.Random(type: CardType.SPELL), new[] { 2, 3, 5 });
        Expect(OfflinePoolFilter.Random(minCost: 3, maxCost: 4), new[] { 1, 3, 4 });
        Expect(OfflinePoolFilter.Random(minCost: 4, maxCost: 4), new[] { 3, 4 });
        Expect(OfflinePoolFilter.Random(minCost: -1, maxCost: -1), Array.Empty<int>());
        Expect(OfflinePoolFilter.Random(minCost: 5, maxCost: 1), Array.Empty<int>());
        Expect(OfflinePoolFilter.Random(race: Race.BEAST), new[] { 0, 1, 4 });
        Expect(OfflinePoolFilter.Random(race: Race.DRAGON), new[] { 1, 4 });
        Expect(OfflinePoolFilter.Random(race: Race.ALL), new[] { 4 });
        Expect(OfflinePoolFilter.Random(race: Race.INVALID), Array.Empty<int>());
        Expect(OfflinePoolFilter.Random(multipleMinionTypes: true), new[] { 1, 4 });
        Expect(OfflinePoolFilter.Random(spellSchool: 4), new[] { 2 });
        Expect(OfflinePoolFilter.Random(spellSchool: 6), new[] { 3 });
        Expect(OfflinePoolFilter.Random(spellSchool: 3), new[] { 5 });
        Expect(OfflinePoolFilter.Random(rarity: Rarity.RARE), new[] { 1, 4 });
        Expect(OfflinePoolFilter.Random(keyword: GameTag.TAUNT), new[] { 1, 3, 5 });
        Expect(OfflinePoolFilter.Random(), new[] { 0, 1, 3, 4, 5 }, source: ids[2]);
        Expect(OfflinePoolFilter.Discover(), new[] { 0, 4 }, source: ids[2]);
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(manifest, CardClass.MAGE,
            OfflinePoolFilter.Random(scope: PoolScope.Past, minCost: 5, maxCost: 5), 68).Count == 2, "Past and source exclusion");

        // Every field participates in equality and hash; dynamic values are ordinary immutable costs.
        var baseline = OfflinePoolFilter.Random();
        var changes = new[] {
            new OfflinePoolFilter(scope: PoolScope.Past), new OfflinePoolFilter(classMode: PoolClassMode.OtherClass),
            new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE),
            new OfflinePoolFilter(type: CardType.MINION), new OfflinePoolFilter(minCost: 1), new OfflinePoolFilter(maxCost: 2),
            new OfflinePoolFilter(race: Race.BEAST), new OfflinePoolFilter(multipleMinionTypes: true),
            new OfflinePoolFilter(spellSchool: 3), new OfflinePoolFilter(rarity: Rarity.RARE), new OfflinePoolFilter(keyword: GameTag.TAUNT)
        };
        foreach (var filter in changes)
        {
            OfflinePoolFixtures.Check(filter != baseline && !filter.Equals((object)baseline) && filter.GetHashCode() != baseline.GetHashCode(), "complete value identity");
            var copy = new OfflinePoolFilter(filter.Scope, filter.ClassMode, filter.ExactClass, filter.Type,
                filter.MinCost, filter.MaxCost, filter.Race, filter.MultipleMinionTypes, filter.SpellSchool, filter.Rarity, filter.Keyword);
            OfflinePoolFixtures.Check(copy == filter && copy.GetHashCode() == filter.GetHashCode(), "equal reconstructed values");
        }
        var mage = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE);
        var druid = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID);
        OfflinePoolFixtures.Check(mage != druid && mage.GetHashCode() != druid.GetHashCode(), "exact class identity");
        foreach (Action invalid in new Action[] {
            () => new OfflinePoolFilter(classMode: PoolClassMode.Exact),
            () => new OfflinePoolFilter(exactClass: CardClass.MAGE),
            () => new OfflinePoolFilter(scope: (PoolScope)99),
            () => new OfflinePoolFilter(classMode: (PoolClassMode)99)
        })
        {
            try { invalid(); throw new Exception("accepted invalid filter"); }
            catch (ArgumentException) { }
        }
        int attack = 3;
        var bound = OfflinePoolFilter.Random(minCost: attack, maxCost: attack);
        attack = 4;
        Expect(bound, new[] { 1 });
        Expect(OfflinePoolFilter.Random(minCost: attack, maxCost: attack), new[] { 3, 4 });
        var locationXml = OfflinePoolFixtures.Fixture();
        locationXml.Root.Element("Members").Elements("Card").First().SetAttributeValue("type", "LOCATION");
        var locationManifest = OfflinePoolFixtures.Manifest(locationXml);
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(locationManifest, CardClass.MAGE,
            OfflinePoolFilter.Random(type: CardType.LOCATION), 0).Select(c => c.AssetId).SequenceEqual(new[] { ids[0] }),
            "location type admitted by explicit filter");
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(locationManifest, CardClass.MAGE,
            OfflinePoolFilter.Random(type: CardType.MINION), 0).Select(c => c.AssetId).SequenceEqual(new[] { ids[1], ids[4] }),
            "random minions exclude location members");
        PilotCounts();
        Console.WriteLine("PASS offline pool filter: all class modes, multiclass/Demon Hunter, printed/resolved costs, secondary/ALL races, school, rarity, positive keyword, source, full equality/hash");
    }

    private static void ColossalDestinations()
    {
        var xml = OfflinePoolFixtures.Fixture(1);
        var row = xml.Root.Element("Members").Elements("Card").First();
        row.SetAttributeValue("cardId", "CS2_168");
        row.SetAttributeValue("registrationKind", "legacy");
        row.SetAttributeValue("dbfId", Cards.FromId("CS2_168").AssetId);
        row.SetAttributeValue("rarity", "LEGENDARY");
        row.SetAttributeValue("cost", 8);
        row.Add(new XElement("Tag", new XAttribute("id", 2247), new XAttribute("value", 1)));
        var manifest = OfflinePoolFixtures.Manifest(xml);
        int id = manifest.Standard.Single().DbfId;
        var filter = OfflinePoolFilter.Random(type: CardType.MINION);
        var random = new Util.DeepCloneableRandom(42);
        OfflinePoolFixtures.Check(OfflineCardPools.Pick(manifest, random, filter, CardClass.MAGE, 0)?.AssetId == id,
            "synthetic admitted Colossal can be generated into hand");
        OfflinePoolFixtures.Check(OfflineCardPools.SampleDistinct(manifest, random, filter, CardClass.MAGE, 0)
            .Single().AssetId == id, "Discover-to-hand includes synthetic Colossal");
        foreach (var destination in new[] { PoolDestination.Summon, PoolDestination.Transform, PoolDestination.Other })
        {
            var before = random.CaptureState();
            OfflinePoolFixtures.Check(OfflineCardPools.Candidates(manifest, CardClass.MAGE, filter, 0, destination).Count == 0,
                "Colossal excluded before sampling for " + destination);
            OfflinePoolFixtures.Check(OfflineCardPools.Pick(manifest, random, filter, CardClass.MAGE, 0, destination) == null &&
                OfflineCardPools.SampleDistinct(manifest, random, filter, CardClass.MAGE, 0, destination).Length == 0,
                "empty Colossal destination creates no result");
            OfflinePoolFixtures.Check(before.Equals(random.CaptureState()), "empty destination consumes no RNG");
        }
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(manifest, CardClass.MAGE, filter, 0).Single().AssetId == id,
            "destination cache does not poison hand pool");
        OfflinePoolFixtures.Check(OfflineCardPools.Pick(manifest, random, filter, CardClass.MAGE, 0, PoolDestination.Deck)?.AssetId == id,
            "off-board deck generation includes ordinary Colossal");
        var union = OfflinePoolFilter.Union(filter, OfflinePoolFilter.Random(type: CardType.SPELL));
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(manifest, CardClass.MAGE, union, 0, PoolDestination.Summon).Count == 0,
            "union and curated filters cannot bypass destination");
        var game = OfflinePoolTestRecipes.NewGame();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, manifest);
        var owner = game.Player1;
        var context = new SabberStoneCore.EffectSystem.EffectContext(game, owner.Id, owner.Hero.Id,
            stack: new TaskStack { Number = 8 });
        var body = context.PickRandomCard(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.LegendaryMinion(), recipeDbfId: 69672);
        OfflinePoolFixtures.Check(body?.Id == "CS2_168", "declared hand picker includes body");
        context.AddToHand(body.Id);
        OfflinePoolFixtures.Check(owner.HandZone.Any(card => card.Card.Id == "CS2_168"), "body delivered into hand");
        OfflinePoolFixtures.Check(context.PickRandomCard(filter, SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber,
            recipeDbfId: 129846) == null, "declared dynamic summon picker excludes body");
        OfflinePoolFixtures.Check(context.PickRandomCard(filter, SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber,
            recipeDbfId: 122963) == null, "declared dynamic transform picker excludes body");
        OfflinePoolFixtures.Check(context.Discover(OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 8, maxCost: 8),
            recipeDbfId: 127473) == false, "Discover-to-summon excludes body before opening choice");
        var declaration = new OfflinePoolDeclaration("proof/colossal", int.MaxValue,
            new PoolRequirement("hand", filter, new[] { "MAGE" }),
            new PoolRequirement("summon", filter, new[] { "MAGE" }, destination: PoolDestination.Summon),
            new PoolRequirement("transform", filter, new[] { "MAGE" }, destination: PoolDestination.Transform));
        OfflinePoolFixtures.Check(OfflinePoolAdmission.Evaluate(manifest, new[] { declaration }, new Dictionary<int,string>())
            .Cards.Single().PoolRequirements.Select(requirement => requirement.Count).SequenceEqual(new[] { 1, 0, 0 }),
            "admission thresholds count each destination separately");
        row.Add(new XElement("Tag", new XAttribute("id", 4538), new XAttribute("value", 1)));
        var herald = OfflinePoolFixtures.Manifest(xml);
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(herald, CardClass.MAGE, filter, 0).Count == 0,
            "Herald restriction remains even for synthetic hand admission");
        row.Elements("Tag").Single(tag => (int)tag.Attribute("id") == 4538).Remove();
        xml.Root.Element("Members").Elements("Card").Where(card => card != row).Remove();
        row.SetAttributeValue("scope", "Past"); row.SetAttributeValue("set", "LEGACY");
        var past = OfflinePoolFixtures.Manifest(xml);
        var curated = new OfflinePoolFilter(scope: PoolScope.Past, cardIds: new[] { "CS2_168" });
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(past, CardClass.MAGE, curated, 0).Single().AssetId == id &&
            OfflineCardPools.Candidates(past, CardClass.MAGE, curated, 0, PoolDestination.Transform).Count == 0,
            "historical curated body keeps the same destination guard");
        try { OfflineCardPools.Candidates(past, CardClass.MAGE, curated, 0, (PoolDestination)99);
            throw new Exception("accepted invalid pool destination"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS Colossal destinations: synthetic admitted body; hand/Discover/deck include; summon/transform/other exclude; cache and empty RNG");
    }

    private static void PilotCounts()
    {
        var manifest = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.M0Catalog());
        // Preserve the independently counted bootstrap fixture as the installed catalog grows.
        var pilots = new (string Source, int DbfId, OfflinePoolFilter Filter, int Count)[] {
            ("CATA_484", 123398, OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 1, maxCost: 1), 23),
            ("CATA_723", 122641, OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4), 51),
            ("BE_854", 129892, OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3), 52),
            ("JAIL_125", 126356, OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 3), 7),
            ("BE_220", 129256, OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 6), 13),
            ("EDR_530", 115364, OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4), 19),
            ("TIME_040", 120782, OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5, scope: PoolScope.Past), 3),
            ("JAIL_328", 126618, new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.PALADIN), 34),
            ("EDR_848", 114914, new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID, type: CardType.SPELL), 14)
        };
        foreach (var pilot in pilots)
        {
            var candidates = OfflineCardPools.Candidates(manifest, CardClass.MAGE, pilot.Filter, pilot.DbfId);
            OfflinePoolFixtures.Check(candidates.Count == pilot.Count && candidates.All(c => c.AssetId != pilot.DbfId),
                $"M0 {pilot.Source}: expected {pilot.Count}, got {candidates.Count}");
        }
        var druidSpells = OfflineCardPools.Candidates(manifest, CardClass.MAGE, pilots[8].Filter, pilots[8].DbfId);
        OfflinePoolFixtures.Check(druidSpells.Any(c => manifest.Standard.Single(m => m.DbfId == c.AssetId).Classes.Count > 1), "M0 Druid spell includes multiclass");
        Console.WriteLine("PASS offline pool M0 pilot counts: 1-Cost spell=23; 4-Cost minion=51; 3-Cost minion=52; Frost=7; Shadow=13; Nature=19; Past 5-Cost minion=3; Paladin=34; Druid spell=14; no deviations");
    }
}
