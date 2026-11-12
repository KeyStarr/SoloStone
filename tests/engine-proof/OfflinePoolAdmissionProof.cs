using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class OfflinePoolAdmissionProof
{
    // Batch 11 converts these existing legacy members to declared Standard generation.
    private static readonly Dictionary<string, Action> LegacyRecipeProofs = new(StringComparer.Ordinal) {
        ["CORE_CFM_781"] = CoreLegacy.CoreShakuTheCollectorProof.Run,
        ["CORE_EX1_189"] = CoreLegacy.BrightwingProof.Run,
        ["CORE_GIL_531"] = CoreLegacy.WitchsApprenticeProof.Run,
        ["CORE_GIL_836"] = CoreLegacy.BlazingInvocationProof.Run,
        ["CORE_KAR_057"] = CoreLegacy.IvoryKnightProof.Run,
        ["CORE_KAR_062"] = CoreLegacy.NetherspiteHistorianProof.Run,
        ["CORE_KAR_069"] = CoreLegacy.CoreSwashburglarProof.Run,
        ["CORE_KAR_077"] = CoreLegacy.SilvermoonPortalProof.Run,
        ["CORE_LOE_039"] = CoreLegacy.GorillabotA3Proof.Run,
        ["CORE_UNG_912"] = CoreLegacy.CoreJeweledMacawProof.Run
    };

    private static void Check(bool value, string message) => OfflinePoolFixtures.Check(value, "admission: " + message);

    internal static void Run()
    {
        var manifest = OfflinePoolPolicy.Load().BuildManifest();
        var report = OfflinePoolAdmission.Evaluate(manifest);
        Check(report.Cards.Count == OfflinePoolDeclarations.Cards.Count
            && report.Cards.Select(c => c.CardId).ToHashSet().SetEquals(OfflinePoolDeclarations.Cards.Select(c => c.CardId))
            && report.Cards.All(c => c.Registered), "all declared recipes registered");
        var pilots = OfflinePoolDeclarations.Pilots.Select(p => report.Cards.Single(c => c.CardId == p.CardId)).ToArray();
        Check(pilots.Length == 9 && pilots.All(c => c.Registered), "nine pilots registered");
        var counted = pilots.Append(report.Cards.Single(c => c.CardId == "JAIL_892")).ToArray();
        Check(counted.Length == 10 && counted.All(c => c.Registered), "nine pilots and Cosmic Manifestations registered");
        // The independent derivation from the client export (OfflinePoolExpectations) is the oracle of this proof: client tier only.
        bool oracle = ClientExport.Oracle;
        if (oracle)
            Check(manifest.Standard.Select(m => m.CardId).SequenceEqual(OfflinePoolExpectations.Ids(PoolScope.Standard))
                && manifest.Past.Select(m => m.CardId).SequenceEqual(OfflinePoolExpectations.Ids(PoolScope.Past)), "membership equals eligibility/export scopes");
        else ClientExport.Skip("Offline pool admission against the independent derivation from the client export");
        foreach (var card in report.Cards)
            foreach (var row in card.PoolRequirements)
            {
                var template = OfflinePoolDeclarations.Cards.Single(c => c.CardId == card.CardId).Pools.Single(p => p.Name == row.Name);
                var filter = row.Binding != "ordinary" ? template.Filter : template.CostBinding == PoolCostBinding.StackNumberMaxCost
                    ? template.Filter.WithMaxCost(template.OrdinaryCost.Value) : template.Filter.WithCost(template.OrdinaryCost.Value);
                if (!oracle)
                {
                    Check(row.Filter == filter && (!OfflinePoolPolicy.Load().AllowsCard(Cards.FromId(card.CardId)) || row.Count >= Math.Min(3, row.MinimumRequiredCandidates)
                        || row.Filter.MinCost.HasValue && row.Filter.MinCost == row.Filter.MaxCost || template.AllowUnimplementedCuratedCards || template.AllowThinExactCost),
                        card.CardId + " admitted filter for " + row.Class + "/" + row.Binding);
                    continue;
                }
                var missing = template.AllowUnimplementedCuratedCards ? template.Filter.CardIds.Where(id =>
                    !Cards.FromId(id).Implemented && !EffectModules.ByDbfId.Values.Contains(id) &&
                    !OfflinePoolExpectations.Ids(PoolScope.Standard).Concat(OfflinePoolExpectations.Ids(PoolScope.Past)).Contains(id)).ToArray() : Array.Empty<string>();
                int minimum = template.AllowThinExactCost ? 0 : template.AllowUnimplementedCuratedCards
                    ? Math.Min(3, template.Filter.CardIds.Count - missing.Length) : 3;
                Check(row.Filter == filter && row.MinimumRequiredCandidates == minimum && row.UnimplementedCuratedCardIds.SequenceEqual(missing) &&
                    (!OfflinePoolPolicy.Load().AllowsCard(Cards.FromId(card.CardId)) || row.Count >= minimum
                        || row.Filter.MinCost.HasValue && row.Filter.MinCost == row.Filter.MaxCost) && row.Count == OfflinePoolExpectations.Count(filter, row.Class, card.DbfId, template.Destination),
                    card.CardId + " independent export/filter count for " + row.Class + "/" + row.Binding);
            }
        foreach (var card in counted)
            Check(Cards.All.Any(c => c.Id == card.CardId && c.AssetId == card.DbfId && c.Implemented && c.ExcludeFromLegacyRandomPools), "exact excluded pilot definition");
        Check(report.Cards.Where(c => !Cards.FromId(c.CardId).ExcludeFromLegacyRandomPools).Select(c => c.CardId)
            .ToHashSet(StringComparer.Ordinal).SetEquals(LegacyRecipeProofs.Keys), "exact batch-11 legacy recipe owners");
        StandardSetsProof.Run(selectedProofs: LegacyRecipeProofs.Values.ToArray());
        foreach (var card in report.Cards)
        {
            bool legacy = LegacyRecipeProofs.TryGetValue(card.CardId, out var proof);
            Check(Cards.All.Any(c => c.Id == card.CardId && c.AssetId == card.DbfId && c.Implemented
                && c.ExcludeFromLegacyRandomPools != legacy), "exact declared recipe definition and legacy membership");
            if (legacy)
                Check(StandardSets.CollectibleIds.Contains(card.CardId) && EffectModules.ByDbfId.GetValueOrDefault(card.DbfId) == card.CardId
                    && PoolProofEvidence.HasPassed(card.CardId, proof), card.CardId + " Standard module with passing per-card proof");
        }
        // Verify declaration identity/class domains against pinned export, including multiclass metadata.
        if (ClientExport.Oracle)
        {
            var export = ClientExport.RequireCards();
            foreach (var declaration in OfflinePoolDeclarations.Cards)
            {
                var card = export[declaration.CardId];
                Check(card.GetProperty("dbfId").GetInt32() == declaration.DbfId, "pinned dbfId");
                var classes = card.GetProperty("cardClasses").EnumerateArray().Select(c => c.GetString()).ToArray();
                if (declaration.CardId == "TIME_EVENT_999t")
                {
                    Check(declaration.Pools.Count == 11 && declaration.Pools.All(r => r.ClassDomain.Count == 1
                        && r.Filter.ClassMode == PoolClassMode.Exact && r.ClassDomain.Single() ==
                            ((int)r.Filter.ExactClass.Value == 14 ? "DEMONHUNTER" : r.Filter.ExactClass.Value.ToString()))
                        && ExpandClasses(declaration.Pools.SelectMany(r => r.ClassDomain)).SetEquals(ExpandClasses(classes)),
                        "rewound Sands has exactly one matching class-spell recipe per exported neutral class");
                }
                else foreach (var requirement in declaration.Pools)
                    Check(ExpandClasses(requirement.ClassDomain).SetEquals(ExpandClasses(classes)), "printed class domain: " + declaration.CardId + "/" + requirement.Name + " declared=" + string.Join(",", requirement.ClassDomain) + " export=" + string.Join(",", classes));
            }
        }
        else ClientExport.Skip("Offline pool declarations against the printed class domains of the client export");
        var neutral = report.Cards.Single(c => c.CardId == "TIME_040").PoolRequirements;
        Check(neutral.Count == 11 && neutral.Any(r => r.Class == "DEATHKNIGHT") && neutral.Any(r => r.Class == "DEMONHUNTER"), "all eleven neutral classes");
        CheckControllerClasses();

        // Hand-counted shortage fixtures remain fixed as real admission grows.
        manifest = PinnedPoolFixture.Manifest;
        var past = OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 6, scope: PoolScope.Past);
        var source = manifest.Past.First();
        var fixture = new OfflinePoolDeclaration(source.CardId, source.DbfId,
            new PoolRequirement("past-five", past, new[] { "NEUTRAL" }));
        Check(OfflineCardPools.Candidates(manifest, CardClass.MAGE, past, 0).Count == 3, "fixture starts at three");
        var unregistered = OfflinePoolAdmission.Evaluate(manifest, new[] { fixture }, new Dictionary<int, string>());
        Check(unregistered.Cards.Single().PoolRequirements.All(r => r.Count == 2), "source exclusion drops three to two");
        unregistered.ValidateRegistered();
        var registered = OfflinePoolAdmission.Evaluate(manifest, new[] { fixture }, new Dictionary<int, string> { [source.DbfId] = source.CardId });
        try { registered.ValidateRegistered(); throw new Exception("accepted underthreshold registered fixture"); }
        catch (InvalidDataException error) { Check(error.Message.Contains("pool too small") && error.Message.Contains("count=2") && error.Message.Contains("past-five"), "actionable failure"); }

        var enough = new OfflinePoolDeclaration("proof/enough", int.MaxValue,
            new PoolRequirement("past-three", past, new[] { "NEUTRAL" }));
        var enoughReport = OfflinePoolAdmission.Evaluate(manifest, new[] { enough }, new Dictionary<int, string> { [enough.DbfId] = enough.CardId });
        Check(enoughReport.Cards.Single().Registered && enoughReport.Cards.Single().PoolRequirements.All(r => r.Count == 3), "registered threshold fixture has exactly three");
        enoughReport.ValidateRegistered();

        var multi = new OfflinePoolDeclaration("proof/multi", int.MaxValue,
            new PoolRequirement("enough", past, new[] { "DRUID", "PRIEST" }),
            new PoolRequirement("empty", OfflinePoolFilter.Random(minCost: 999), new[] { "DRUID", "PRIEST" }));
        var multiReport = OfflinePoolAdmission.Evaluate(manifest, new[] { multi }, new Dictionary<int, string>());
        Check(multiReport.Cards.Single().PoolRequirements.Select(r => (r.Name, r.Class, r.Count)).SequenceEqual(new[] {
            ("enough", "DRUID", 3), ("enough", "PRIEST", 3), ("empty", "DRUID", 0), ("empty", "PRIEST", 0) }), "each pool and multiclass separately");
        multiReport.ValidateRegistered();
        RejectRegistered(manifest, multi, "filter=empty", "class=DRUID", "class=PRIEST", "binding=fixed", "count=0");

        var dynamic = new OfflinePoolDeclaration("proof/dynamic", int.MaxValue,
            new PoolRequirement("dynamic-past", OfflinePoolFilter.Random(type: CardType.MINION, scope: PoolScope.Past),
                new[] { "MAGE" }, PoolCostBinding.StackNumber, 5));
        var dynamicRows = OfflinePoolAdmission.Evaluate(manifest, new[] { dynamic }, new Dictionary<int, string>()).Cards.Single().PoolRequirements;
        Check(dynamicRows.Count == 2 && dynamicRows[0].Binding == "static-envelope" && dynamicRows[1].Binding == "ordinary" &&
            dynamicRows[0].Count == 5 && dynamicRows[1].Count == 3 && dynamicRows[1].Filter.MinCost == 5 && dynamicRows[1].Filter.MaxCost == 5, "dynamic envelope and ordinary binding");
        OfflinePoolAdmission.Evaluate(manifest, new[] { dynamic }, new Dictionary<int, string> { [dynamic.DbfId] = dynamic.CardId }).ValidateRegistered();
        var narrow = new OfflinePoolDeclaration("proof/dynamic-empty", int.MaxValue,
            new PoolRequirement("dynamic-empty", OfflinePoolFilter.Random(type: CardType.MINION, scope: PoolScope.Past),
                new[] { "MAGE" }, PoolCostBinding.StackNumber, 4));
        var narrowRows = OfflinePoolAdmission.Evaluate(manifest, new[] { narrow }, new Dictionary<int, string>()).Cards.Single().PoolRequirements;
        Check(narrowRows[0].Count == 5 && narrowRows[1].Count == 0, "D2 exact ordinary bucket may be empty");
        OfflinePoolAdmission.Evaluate(manifest, new[] { narrow }, new Dictionary<int, string> { [narrow.DbfId] = narrow.CardId }).ValidateRegistered();
        var emptyEnvelope = new OfflinePoolDeclaration("proof/empty-envelope", int.MaxValue,
            new PoolRequirement("empty-envelope", OfflinePoolFilter.Random(type: CardType.MINION, maxCost: 2, scope: PoolScope.Past),
                new[] { "MAGE" }, PoolCostBinding.StackNumber, 4));
        RejectRegistered(manifest, emptyEnvelope, "filter=empty-envelope", "binding=static-envelope", "count=1");
        foreach (Action invalid in new Action[] {
            () => new PoolRequirement("bad", past, new[] { "MAGE" }, PoolCostBinding.StackNumber),
            () => new PoolRequirement("bad", past, new[] { "MAGE" }, ordinaryCost: 5),
            () => new PoolRequirement("bad", past, new[] { "INVALID" }),
            () => new PoolRequirement("", past, new[] { "MAGE" })
        })
        {
            try { invalid(); throw new Exception("accepted invalid requirement"); }
            catch (ArgumentException) { }
        }
        Directory.CreateDirectory("sim/out");
        File.WriteAllText("sim/out/offline-pool-requirements.json", JsonSerializer.Serialize(report,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n");
        // An unadmitted integration slice must not hide unrelated admission fixture failures.
        report.ValidateRegistered();
        Console.WriteLine($"PASS offline pool admission: {report.Cards.Count} registered recipes; independent export counts for every declared filter/class/binding; eleven distinct controller-class counts; registered three accepted; multi-pool/dynamic shortages and source-exclusion 3->2 rejected; JSON report");
    }

    private static HashSet<string> ExpandClasses(IEnumerable<string> classes) => classes.SelectMany(c => c == "NEUTRAL"
        ? new[] { "DEATHKNIGHT", "DEMONHUNTER", "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" }
        : new[] { c }).ToHashSet(StringComparer.Ordinal);

    private static void CheckControllerClasses()
    {
        string[] classes = { "DEATHKNIGHT", "DEMONHUNTER", "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" };
        var xml = OfflinePoolFixtures.Catalog();
        var members = xml.Root.Element("Members");
        var cards = members.Elements("Card").Where(c => (string)c.Attribute("scope") == "Standard").Take(12).ToArray();
        members.RemoveNodes();
        // One neutral plus nested class memberships gives each hero a distinct hand-counted pool.
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Elements("Class").Remove();
            cards[i].Elements("Dependency").Remove();
            foreach (string hero in i == 0 ? new[] { "NEUTRAL" } : classes.Skip(i - 1))
                cards[i].Add(new XElement("Class", new XAttribute("name", hero)));
            members.Add(cards[i]);
        }
        xml.Root.Element("Dependencies").RemoveNodes();
        var fixture = new OfflinePoolDeclaration("proof/controller", int.MaxValue,
            new PoolRequirement("controller-and-neutral", OfflinePoolFilter.Discover(), new[] { "NEUTRAL" }));
        var rows = OfflinePoolAdmission.Evaluate(OfflinePoolFixtures.Manifest(xml), new[] { fixture }, new Dictionary<int, string>()).Cards.Single().PoolRequirements;
        Check(rows.Select(r => (r.Class, r.Count)).SequenceEqual(new[] {
            ("DEATHKNIGHT", 2), ("DEMONHUNTER", 3), ("DRUID", 4), ("HUNTER", 5), ("MAGE", 6), ("PALADIN", 7),
            ("PRIEST", 8), ("ROGUE", 9), ("SHAMAN", 10), ("WARLOCK", 11), ("WARRIOR", 12) }), "controller class selects its own candidates across all eleven classes");
    }

    private static void RejectRegistered(OfflinePoolManifest manifest, OfflinePoolDeclaration fixture, params string[] details)
    {
        var report = OfflinePoolAdmission.Evaluate(manifest, new[] { fixture }, new Dictionary<int, string> { [fixture.DbfId] = fixture.CardId });
        Check(report.Cards.Single().Registered, fixture.CardId + " is registered");
        try { report.ValidateRegistered(); throw new Exception("accepted underthreshold " + fixture.CardId); }
        catch (InvalidDataException error)
        {
            Check(error.Message.Contains("pool too small") && details.All(error.Message.Contains), fixture.CardId + " actionable rejection: " + error.Message);
        }
    }
}
