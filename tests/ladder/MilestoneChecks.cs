using System.Text.Json;

internal static class MilestoneChecks
{
    private static readonly string[] FloorIds = {
        "floor:bronze-5", "floor:silver-10", "floor:silver-5", "floor:gold-10", "floor:gold-5",
        "floor:platinum-10", "floor:platinum-5", "floor:diamond-10", "floor:diamond-5", "legend"
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Milestones: " + message);
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (InvalidOperationException) { return; }
        throw new Exception("Milestones: expected rejection for " + message);
    }

    internal static void Run()
    {
        var catalog = MilestoneCatalog.V1;
        LocalLadderMilestones.Validate(catalog);
        CatalogueMatchesJson(catalog);
        Floors(catalog);
        LegendNumbers(catalog);
        DynamicGrants(catalog);
        CatchUp(catalog);
        Totals(catalog);
        Validation(catalog);
        Check(catalog.Milestones.Select(m => LocalLadderMilestones.DisplayName(m.Id)).SequenceEqual(new[] {
            "Bronze 5", "Silver", "Silver 5", "Gold", "Gold 5", "Platinum", "Platinum 5", "Diamond", "Diamond 5",
            "Legend", "Top 100", "Legend #1" }) && LocalLadderMilestones.DisplayName("floor:x-7") == "floor:x-7"
            && LocalLadderMilestones.DisplayName(null) == null, "display names");
        Console.WriteLine("Milestone checks passed.");
    }

    private static void CatalogueMatchesJson(MilestoneCatalog catalog)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "milestones-v1.json")));
        var root = json.RootElement;
        Check(root.GetProperty("version").GetInt32() == catalog.Version, "JSON catalogue version drift");
        Check(root.GetProperty("deckKitGoldCap").GetInt32() == catalog.DeckKitGoldCap, "JSON kit cap drift");
        var entries = root.GetProperty("milestones").EnumerateArray().ToArray();
        Check(entries.Length == catalog.Milestones.Count, "JSON catalogue length drift");
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var actual = catalog.Milestones[i];
            Check(entry.GetProperty("id").GetString() == actual.Id
                && entry.GetProperty("kind").GetString() == actual.Kind
                && entry.GetProperty("threshold").GetInt32() == actual.Threshold
                && entry.GetProperty("isLeague").GetBoolean() == actual.IsLeague, "JSON milestone drift: " + actual.Id);
            var grants = entry.GetProperty("grants").EnumerateArray().ToArray();
            Check(grants.Length == actual.Grants.Count, "JSON grant count drift: " + actual.Id);
            for (int g = 0; g < grants.Length; g++)
                Check(grants[g].GetProperty("Kind").GetString() == actual.Grants[g].Kind
                    && grants[g].GetProperty("Ref").GetString() == actual.Grants[g].Ref
                    && grants[g].GetProperty("Count").GetInt32() == actual.Grants[g].Count,
                    "JSON grant drift: " + actual.Id);
        }
    }

    private static void Floors(MilestoneCatalog catalog)
    {
        string[] expectedGrants = {
            "Pack:STANDARD:2",
            "Pack:STANDARD:3|Token:EPIC:1|CardBack:cardback:ladder-silver:1",
            "Pack:STANDARD:2",
            "Pack:STANDARD:3|DeckKit:kit:ladder-gold:1|CardBack:cardback:ladder-gold:1",
            "Pack:STANDARD:3",
            "Pack:STANDARD:4|Token:LEGENDARY:1|CardBack:cardback:ladder-platinum:1",
            "Pack:STANDARD:3",
            "Pack:STANDARD:4|Token:LEGENDARY:1|CardBack:cardback:ladder-diamond:1",
            "Pack:STANDARD:2|Token:LEGENDARY:1",
            "CardBack:cardback:ladder-legend:1|Token:LEGENDARY:1|Trophy:trophy:legend:1|LegendDeck:Snapshot:1"
        };
        var earned = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < FloorIds.Length; i++)
        {
            int threshold = (i + 1) * 15;
            Check(LocalLadderMilestones.Evaluate(threshold - 2, threshold - 1, null, earned, catalog, SnapshotOnly.Instance).Count == 0,
                "floor paid early: " + FloorIds[i]);
            var rewards = LocalLadderMilestones.Evaluate(threshold - 1, threshold, null, earned, catalog, SnapshotOnly.Instance);
            Check(rewards.Count == 1 && rewards[0].MilestoneId == FloorIds[i] && rewards[0].FirstTime,
                "first floor reach: " + FloorIds[i]);
            Check(string.Join("|", rewards[0].Grants.Select(g => g.Kind + ":" + g.Ref + ":" + g.Count)) == expectedGrants[i],
                "exact floor rewards: " + FloorIds[i]);
            Check(earned.Count == i, "evaluation must not consume earned ids");
            earned.Add(FloorIds[i]);
            Check(LocalLadderMilestones.Evaluate(threshold - 1, threshold, null, earned, catalog, SnapshotOnly.Instance).Count == 0,
                "floor paid twice: " + FloorIds[i]);
        }
        var firstTwo = new[] { "floor:bronze-5", "floor:silver-10" };
        var jump = LocalLadderMilestones.Evaluate(40, 135, null, firstTwo, catalog, null);
        Check(jump.Select(r => r.MilestoneId).SequenceEqual(new[] {
            "floor:silver-5", "floor:gold-10", "floor:gold-5", "floor:platinum-10",
            "floor:platinum-5", "floor:diamond-10", "floor:diamond-5"
        }), "40 to 135 jump order and Diamond 5 stop");
        Check(JsonSerializer.Serialize(jump) == JsonSerializer.Serialize(LocalLadderMilestones.Evaluate(40, 135, null,
            firstTwo.Reverse().ToArray(), catalog, null)), "earned enumeration order must not affect rewards");
        Check(LocalLadderMilestones.Evaluate(0, 135, null, Array.Empty<string>(), catalog, null).Count == 9,
            "Diamond 5 must not grant Legend");
        Check(LocalLadderMilestones.Evaluate(0, 150, null, earned, catalog, null).Count == 0,
            "simulated reset must not return repeat promotions");
        foreach (var transition in new[] { (44, 43), (44, 44), (45, 45), (46, 45) })
            Check(LocalLadderMilestones.Evaluate(transition.Item1, transition.Item2, null,
                new[] { "floor:bronze-5", "floor:silver-10", "floor:silver-5" }, catalog, null).Count == 0,
                "loss/tie/abandon must not pay earned floors");
        Check(LocalLadderMilestones.Evaluate(61, 60, null, firstTwo, catalog, null).Count == 0,
            "a loss above unearned floors pays nothing (catch-up owns them)");
        Check(LocalLadderMilestones.Evaluate(60, 60, null, firstTwo, catalog, null).Count == 0,
            "a tie at an unearned threshold pays nothing");
        Check(LocalLadderMilestones.Evaluate(29, 46, null, firstTwo, catalog, null).Select(r => r.MilestoneId)
            .SequenceEqual(new[] { "floor:silver-5" }), "after a reset, crossing an unearned floor pays it once");
    }

    private static void LegendNumbers(MilestoneCatalog catalog)
    {
        Check(LocalLadderMilestones.Evaluate(150, 150, null, FloorIds, catalog, null).Count == 0,
            "Legend number must exist");
        Check(LocalLadderMilestones.Evaluate(150, 150, 101, FloorIds, catalog, null).Count == 0,
            "top 100 must not pay at 101");
        Check(LocalLadderMilestones.Evaluate(134, 149, 1, FloorIds, catalog, null).Count == 0,
            "Legend-number grant requires Legend");
        Check(LocalLadderMilestones.Evaluate(150, 150, 100, FloorIds, catalog, null).Single().MilestoneId == "legend:top-100",
            "top 100 on unchanged rank (tie, loss, recovery)");
        Check(LocalLadderMilestones.Evaluate(150, 150, 2, FloorIds, catalog, null).Single().MilestoneId == "legend:top-100",
            "#1 must not pay at #2");
        var all = LocalLadderMilestones.Evaluate(149, 150, 1, FloorIds.Take(9).ToArray(), catalog, new MatchContext());
        Check(all.Select(r => r.MilestoneId).SequenceEqual(new[] { "legend", "legend:top-100", "legend:1" })
            && all.All(r => r.FirstTime), "Legend, top 100, #1 in one evaluation");
        Check(all[1].Grants.Single().Ref == "cardback:ladder-elite"
            && all[2].Grants.Select(g => g.Kind + ":" + g.Ref + ":" + g.Count).SequenceEqual(new[] {
                "CardBack:cardback:ladder-champion:1", "Trophy:trophy:legend-1:1"
            }), "top 100 and #1 exact rewards");
        var earned = FloorIds.Concat(all.Select(r => r.MilestoneId)).ToArray();
        Check(LocalLadderMilestones.Evaluate(150, 150, 1, earned, catalog, null).Count == 0,
            "Legend numbers never pay twice");
        foreach (int invalid in new[] { 0, -1 })
            Reject(() => LocalLadderMilestones.Evaluate(150, 150, invalid, FloorIds, catalog, null), "invalid Legend number");
    }

    private static void DynamicGrants(MilestoneCatalog catalog)
    {
        var context = new MatchContext();
        var rewards = LocalLadderMilestones.Evaluate(0, 150, 1, Array.Empty<string>(), catalog, context);
        Check(rewards.All(r => r.OpponentNpcId == "npc:firebat" && r.OpponentName == "Firebat"), "opponent captured");
        var recipes = rewards.SelectMany(r => r.Grants).Where(g => g.Kind == "Recipe").ToArray();
        Check(recipes.Length == 5 && recipes.All(g => g.Ref == "recipe:control" && g.Count == 1), "resolved recipe per league");
        Check(rewards.Where(r => r.Grants.Any(g => g.Kind == "Challengers")).Select(r => r.MilestoneId)
            .SequenceEqual(new[] { "floor:silver-10", "floor:gold-10", "floor:platinum-10",
                "floor:diamond-10", "floor:diamond-5", "legend" }), "challengers at leagues and Diamond 5");
        var diamond5 = rewards.Single(r => r.MilestoneId == "floor:diamond-5");
        Check(!diamond5.Grants.Any(g => g.Kind == "Recipe") && diamond5.Grants.Where(g => g.Kind == "Challengers")
            .Select(g => g.Ref).SequenceEqual(new[] { "floor:diamond-5:npc-a", "floor:diamond-5:npc-b" }),
            "challenger ids fixed in resolver order, one grant per NPC");
        Check(rewards.Single(r => r.MilestoneId == "legend").Grants.Single(g => g.Kind == "LegendDeck").Ref == "Snapshot",
            "Legend snapshot marker");
        string first = JsonSerializer.Serialize(rewards);
        Check(first == JsonSerializer.Serialize(LocalLadderMilestones.Evaluate(0, 150, 1, Array.Empty<string>(), catalog, context)),
            "deterministic evaluation");
        context.Recipe = null;
        context.HasLegendDeckSnapshot = false;
        context.Challengers.Clear();
        Check(first == JsonSerializer.Serialize(rewards), "receipt grants must not follow context mutations");
        Check(LocalLadderMilestones.Evaluate(149, 150, null, FloorIds.Take(9).ToArray(), catalog, context).Single()
            .Grants.Single(g => g.Kind == "LegendDeck").Ref == "Legacy", "unresolvable Legend deck falls back to Legacy, never blocks");
        context.HasLegendDeckSnapshot = true;
        var empty = LocalLadderMilestones.Evaluate(0, 150, null, Array.Empty<string>(), catalog, context);
        Check(empty.SelectMany(r => r.Grants).All(g => g.Kind != "Recipe" && g.Kind != "Challengers"),
            "missing recipe and empty challengers omitted");
        Check(empty.Last().Grants.Single(g => g.Kind == "LegendDeck").Ref == "Snapshot", "live Legend keeps Snapshot");
        var defaultContext = LocalLadderMilestones.Evaluate(0, 135, null, Array.Empty<string>(), catalog, null);
        Check(defaultContext.All(r => r.OpponentNpcId == null && r.OpponentName == null)
            && defaultContext.SelectMany(r => r.Grants).All(g => g.Kind != "Recipe" && g.Kind != "Challengers"),
            "null context safely omits dynamic NPC grants");
    }

    private static void CatchUp(MilestoneCatalog catalog)
    {
        Check(LocalLadderMilestones.EvaluateCatchUp(14, Array.Empty<string>(), catalog).Count == 0, "catch-up before first floor");
        var rewards = LocalLadderMilestones.EvaluateCatchUp(150, new[] { "floor:bronze-5" }, catalog);
        Check(rewards.Select(r => r.MilestoneId).SequenceEqual(FloorIds.Skip(1)), "catch-up floors only in order");
        Check(rewards.All(r => r.FirstTime && r.OpponentNpcId == null && r.OpponentName == null)
            && rewards.SelectMany(r => r.Grants).All(g => g.Kind != "Recipe" && g.Kind != "Challengers"),
            "catch-up excludes invented match context");
        Check(rewards.Last().Grants.Single(g => g.Kind == "LegendDeck").Ref == "Legacy", "catch-up Legacy deck");
        Check(LocalLadderMilestones.EvaluateCatchUp(150, FloorIds, catalog).Count == 0, "catch-up idempotency");
        Check(LocalLadderMilestones.EvaluateCatchUp(135, Array.Empty<string>(), catalog).Count == 9, "catch-up Diamond 5 stop");
    }

    private static void Totals(MilestoneCatalog catalog)
    {
        var totals = LocalLadderMilestones.Totals(catalog);
        int Count(string kind, string reference = null) => totals.Where(g => g.Kind == kind && (reference == null || g.Ref == reference)).Sum(g => g.Count);
        Check(Count("Pack", "STANDARD") == 26 && Count("Token", "EPIC") == 1 && Count("Token", "LEGENDARY") == 4
            && Count("DeckKit") == 1 && Count("CardBack") == 5 && catalog.DeckKitGoldCap == 3200, "through-Legend totals");
        Check(LocalLadderMilestones.Totals(catalog, true).Where(g => g.Kind == "CardBack").Sum(g => g.Count) == 7,
            "seven card backs including Legend numbers");
    }

    private static void Validation(MilestoneCatalog catalog)
    {
        void Changed(int index, Func<MilestoneDefinition, MilestoneDefinition> change, string message)
        {
            var entries = catalog.Milestones.ToArray();
            entries[index] = change(entries[index]);
            Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200, entries)), message);
        }
        Changed(1, m => new MilestoneDefinition(catalog.Milestones[0].Id, m.Kind, m.Threshold, m.IsLeague, m.Grants), "duplicate id");
        Changed(0, m => new MilestoneDefinition(m.Id, m.Kind, 14, m.IsLeague, m.Grants), "bad floor threshold");
        Changed(1, m => new MilestoneDefinition(m.Id, m.Kind, 15, m.IsLeague, m.Grants), "duplicate floor threshold");
        Changed(0, m => new MilestoneDefinition(m.Id, "Unknown", m.Threshold, m.IsLeague, m.Grants), "bad milestone kind");
        Changed(0, m => new MilestoneDefinition("floor:bronze-10", m.Kind, m.Threshold, m.IsLeague, m.Grants), "invalid milestone id");
        Changed(1, m => new MilestoneDefinition(m.Id, m.Kind, m.Threshold, false, m.Grants), "missing league flag");
        Changed(8, m => new MilestoneDefinition(m.Id, m.Kind, m.Threshold, true, m.Grants), "extra league flag");
        Changed(11, m => new MilestoneDefinition(m.Id, m.Kind, 100, m.IsLeague, m.Grants), "duplicate Legend threshold");
        var reversed = catalog.Milestones.ToArray();
        (reversed[10], reversed[11]) = (reversed[11], reversed[10]);
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200, reversed)), "Legend difficulty order");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200, catalog.Milestones.Skip(1))), "missing floor");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200,
            catalog.Milestones.Skip(10).Concat(catalog.Milestones.Take(10)))), "Legend numbers before floors");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200, catalog.Milestones.Take(11))), "missing #1");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 3200,
            catalog.Milestones.Where(m => m.Id != "legend:top-100"))), "missing top 100");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(1, 0, catalog.Milestones)), "zero kit cap");
        Reject(() => LocalLadderMilestones.Validate(new MilestoneCatalog(0, 3200, catalog.Milestones)), "zero version");
        foreach (var grant in new[] {
            new MilestoneGrant("Gold", "STANDARD", 1), new MilestoneGrant("Pack", "CLASSIC", 1),
            new MilestoneGrant("Token", "FREE", 1), new MilestoneGrant("CardBack", "123", 1),
            new MilestoneGrant("Trophy", "", 1), new MilestoneGrant("DeckKit", "", 1),
            new MilestoneGrant("Recipe", "", 1), new MilestoneGrant("Challengers", "", 1),
            new MilestoneGrant("LegendDeck", "", 1), new MilestoneGrant("Pack", "STANDARD", 0),
            new MilestoneGrant("Pack", "STANDARD", -1)
        })
            Changed(0, m => new MilestoneDefinition(m.Id, m.Kind, m.Threshold, m.IsLeague, new[] { grant }),
                "bad grant " + grant.Kind + "/" + grant.Ref + "/" + grant.Count);
        Reject(() => LocalLadderMilestones.Evaluate(-1, 0, null, Array.Empty<string>(), catalog, null), "negative before stars");
        Reject(() => LocalLadderMilestones.Evaluate(0, -1, null, Array.Empty<string>(), catalog, null), "negative after stars");
        Reject(() => LocalLadderMilestones.EvaluateCatchUp(-1, Array.Empty<string>(), catalog), "negative best stars");
        Reject(() => LocalLadderMilestones.Evaluate(0, 15, null, null, catalog, null), "missing earned ids");
        Reject(() => LocalLadderMilestones.EvaluateCatchUp(15, null, catalog), "missing catch-up earned ids");
        Reject(() => LocalLadderMilestones.Validate(null), "missing catalogue");
    }

    private sealed class SnapshotOnly : IMilestoneContext
    {
        internal static readonly SnapshotOnly Instance = new SnapshotOnly();
        public string OpponentNpcId => null;
        public string OpponentName => null;
        public bool HasLegendDeckSnapshot => true;
        public string ResolveOpponentRecipe() => null;
        public IReadOnlyList<string> ChallengersFor(string milestoneId) => Array.Empty<string>();
    }

    private sealed class MatchContext : IMilestoneContext
    {
        public string OpponentNpcId => "npc:firebat";
        public string OpponentName => "Firebat";
        public string Recipe = "recipe:control";
        public bool HasLegendDeckSnapshot { get; set; } = true;
        public readonly List<string> Challengers = new List<string> { "npc-a", "npc-b" };
        public string ResolveOpponentRecipe() => Recipe;
        public IReadOnlyList<string> ChallengersFor(string milestoneId) => Challengers.Select(id => milestoneId + ":" + id).ToArray();
    }
}
