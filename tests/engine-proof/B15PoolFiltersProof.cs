using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B15PoolFiltersProof
{
    [GroupProof("b15-pool-filters")]
    internal static void Run()
    {
        var xml = OfflinePoolFixtures.Fixture();
        var rows = xml.Root.Element("Members").Elements("Card").Where(r => (string)r.Attribute("scope") == "Standard").ToArray();
        for (int i = 0; i < rows.Length; i++) rows[i].SetAttributeValue("attack", i);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        var all = OfflineCardPools.Candidates(manifest, CardClass.DRUID, OfflinePoolFilter.Random(), 0).ToArray();
        var curated = new OfflinePoolFilter(cardIds: new[] { all[3].Id, all[1].Id, all[3].Id });
        var input = new[] { all[1].Id, all[3].Id };
        var isolated = new OfflinePoolFilter(cardIds: input);
        input[0] = all[0].Id;
        Core2026Proof.Check(isolated == curated, "curated filter defensively copies caller input");
        var threeIds = all.Take(3).Select(c => c.Id).ToArray();
        var fixedThree = new OfflinePoolFilter(cardIds: threeIds);
        foreach (Action invalid in new Action[] {
            () => new OfflinePoolFilter(attackParity: 2), () => new OfflinePoolFilter(costParity: -1),
            () => new OfflinePoolFilter(cardIds: new[] { "bad,id" }),
            () => OfflinePoolFilter.Union(),
            () => OfflinePoolFilter.Union(OfflinePoolFilter.Random(), OfflinePoolFilter.Random(scope: PoolScope.Past)),
            () => new PoolRequirement("unbounded", OfflinePoolFilter.Random(), new[] { "MAGE" }, allowThinExactCost: true),
            () => new PoolRequirement("range-binding", OfflinePoolFilter.Random().WithCost(11), new[] { "MAGE" },
                PoolCostBinding.StackNumberMaxCost, 12, allowThinExactCost: true),
            () => new PoolRequirement("open", OfflinePoolFilter.Random(), new[] { "MAGE" }, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("empty", new OfflinePoolFilter(cardIds: Array.Empty<string>()), new[] { "MAGE" }, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("inherently-thin", curated, new[] { "MAGE" }, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("constrained", fixedThree.WithCost(999), new[] { "MAGE" }, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("dynamic", fixedThree, new[] { "MAGE" }, PoolCostBinding.StackNumber, 1, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("past", new OfflinePoolFilter(scope: PoolScope.Past, cardIds: threeIds), new[] { "MAGE" }, allowUnimplementedCuratedCards: true),
            () => new PoolRequirement("union", new OfflinePoolFilter(cardIds: threeIds, anyOf: new[] { fixedThree }), new[] { "MAGE" }, allowUnimplementedCuratedCards: true) })
        {
            bool rejected = false;
            try { invalid(); } catch (ArgumentException) { rejected = true; }
            Core2026Proof.Check(rejected, "invalid extension predicates/exemption fail closed");
        }
        var union = OfflinePoolFilter.Union(curated, OfflinePoolFilter.Random().WithCost(1));
        var expected = all.Where(c => new[] { all[0].Id, all[1].Id, all[2].Id, all[3].Id }.Contains(c.Id)).ToArray();
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, union, 0).Select(c => c.Id).SequenceEqual(expected.Select(c => c.Id)), "union deduplicates without reordering catalog");
        Core2026Proof.Check(curated == new OfflinePoolFilter(cardIds: new[] { all[1].Id, all[3].Id }), "curated set value identity");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, new OfflinePoolFilter(attackParity: 1), 0).Select(c => c.Id)
            .SequenceEqual(new[] { all[1].Id, all[3].Id, all[5].Id }), "odd printed Attack includes only matching parity");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, new OfflinePoolFilter(exactAttack: 4), 0).Single().Id == all[4].Id, "exact Attack independent of Cost");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, new OfflinePoolFilter(costParity: 0), 0).Select(c => c.Id)
            .SequenceEqual(new[] { all[3].Id, all[4].Id }), "even printed Cost");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, union.WithCost(1), all[0].AssetId).Single().Id == all[2].Id, "bound root filter and source exclusion apply to union");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, new OfflinePoolFilter(cardIds: Array.Empty<string>()), 0).Count == 0, "empty curated list means empty");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.DRUID, union.WithCost(999), 0).Count == 0, "empty exact tier never broadens");
        foreach (var filter in new[] { curated, union, new OfflinePoolFilter(exactAttack: 1), new OfflinePoolFilter(attackParity: 0), new OfflinePoolFilter(costParity: 1) })
            Core2026Proof.Check(filter != default && filter.GetHashCode() != default(OfflinePoolFilter).GetHashCode(), "new predicates participate in cache identity");
        var rng = new Util.DeepCloneableRandom(83); var expectedRng = new Util.DeepCloneableRandom(83);
        for (int i = 0; i < 16; i++)
            Core2026Proof.Check(OfflineCardPools.Pick(manifest, rng, union, CardClass.DRUID, 0).Id == expected[expectedRng.Next(expected.Length)].Id, "seeded ordered random pick vector");
        var offers = OfflineCardPools.SampleDistinct(manifest, new Util.DeepCloneableRandom(17), OfflinePoolFilter.Random(), CardClass.DRUID, 0);
        Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Id).Distinct().Count() == 3, "union Discover distinct");
        var registry = new Dictionary<int,string> { [all[0].AssetId] = all[0].Id };
        var thin = new PoolRequirement("empty-exact", OfflinePoolFilter.Random(type: CardType.MINION).WithCost(999), new[]{"MAGE"}, allowThinExactCost: true);
        OfflinePoolAdmission.Evaluate(manifest, new[]{new OfflinePoolDeclaration(all[0].Id, all[0].AssetId, thin)}, registry).ValidateRegistered();
        CuratedAdmission(manifest, all[1]);
        var installed = OfflinePoolPolicy.Load().BuildManifest();
        foreach (var bucket in new[] {
            ("minion", OfflinePoolFilter.Random(type: CardType.MINION)),
            ("demon", OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON)),
            ("spell", OfflinePoolFilter.Random(type: CardType.SPELL)) })
        {
            var costs = installed.Standard.Where(m => m.Type == (bucket.Item1 == "spell" ? "SPELL" : "MINION"))
                .Select(m => m.Cost).Concat(Enumerable.Range(0, 13)).Distinct().OrderBy(c => c);
            Console.WriteLine("B15 buckets " + bucket.Item1 + ": " + string.Join(" ", costs.Select(cost => cost + "=" +
                OfflineCardPools.Candidates(installed, CardClass.ROGUE, bucket.Item2.WithCost(cost), 0).Count)));
        }
        foreach (var source in SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Where(d =>
            new[]{"BE_298","CATA_621","DINO_427","EDR_888","FIR_920","TLC_824"}.Contains(d.CardId)))
            Console.WriteLine("B15 pools " + source.CardId + ": " + string.Join(" ", source.Pools.Select(pool => pool.Name + "=" +
                OfflineCardPools.Candidates(installed, PoolRequirement.HeroClass(pool.ClassDomain[0] == "NEUTRAL" ? "ROGUE" : pool.ClassDomain[0]), pool.Filter, source.DbfId).Count)));
        Console.WriteLine("PASS B15 pool filters: union, immutable curated identity, parity, exact Attack/Cost, seeded ordering, distinct offers, D2 empty closure");
    }

    private static void CuratedAdmission(OfflinePoolManifest manifest, Card implemented)
    {
        var pending = Cards.All.Where(card => card.Collectible && card.AssetId > 0 && !card.Implemented
            && !SabberStoneCore.CardSets.Client.EffectModules.ByDbfId.ContainsKey(card.AssetId)
            && !manifest.Standard.Concat(manifest.Past).Any(member => member.CardId == card.Id))
            .OrderBy(card => card.Id, StringComparer.Ordinal).Take(3).Select(card => card.Id).ToArray();
        Core2026Proof.Check(pending.Length == 3, "curated fixture needs three unimplemented retail identities");
        var source = new OfflinePoolDeclaration("proof/curated", int.MaxValue,
            new PoolRequirement("curated", new OfflinePoolFilter(cardIds: pending.Take(3).Append(implemented.Id)),
                new[] { "MAGE" }, allowUnimplementedCuratedCards: true));
        var registry = new Dictionary<int, string> { [source.DbfId] = source.CardId };
        var report = OfflinePoolAdmission.Evaluate(manifest, new[] { source }, registry);
        var row = report.Cards.Single().PoolRequirements.Single();
        Core2026Proof.Check(row.Count == 1 && row.MinimumRequiredCandidates == 1 &&
            row.UnimplementedCuratedCardIds.SequenceEqual(pending.Take(3)), "three unavailable members leave one required real candidate");
        report.ValidateRegistered();
        var empty = OfflinePoolFixtures.Fixture(0);
        var withoutImplemented = OfflinePoolFixtures.Manifest(empty);
        Reject(withoutImplemented, registry, "implemented-but-unadmitted member cannot retain exemption");
        for (int i = 0; i < 3; i++)
        {
            registry[Cards.FromId(pending[i]).AssetId] = pending[i];
            var grown = OfflinePoolAdmission.Evaluate(manifest, new[] { source }, registry).Cards.Single().PoolRequirements.Single();
            Core2026Proof.Check(grown.MinimumRequiredCandidates == Math.Min(3, i + 2) && grown.UnimplementedCuratedCardIds.Count == 2 - i,
                "exemption lapses per newly implemented member, ordinary minimum restored at three");
            Reject(manifest, registry, "registration alone cannot waive missing candidate admission");
        }
        var unknown = new OfflinePoolDeclaration("proof/invalid-curated", int.MaxValue,
            new PoolRequirement("invalid", new OfflinePoolFilter(cardIds: pending.Take(2).Append("not-a-retail-card")), new[] { "MAGE" },
                allowUnimplementedCuratedCards: true));
        bool rejected = false;
        try { OfflinePoolAdmission.Evaluate(manifest, new[] { unknown }, registry); }
        catch (System.IO.InvalidDataException) { rejected = true; }
        Core2026Proof.Check(rejected, "unknown identities cannot claim pending-retail exemption");

        void Reject(OfflinePoolManifest fixture, Dictionary<int, string> modules, string message)
        {
            bool failed = false;
            try { OfflinePoolAdmission.Evaluate(fixture, new[] { source }, modules).ValidateRegistered(); }
            catch (System.IO.InvalidDataException) { failed = true; }
            Core2026Proof.Check(failed, message);
        }
    }
}

internal static class B15PoolTest
{
    internal static void WithMembers(Game game, string[] ids, Action action)
    {
        var installed = game.OfflinePools;
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        rows.Elements("Card").Where(r => !ids.Contains((string)r.Attribute("cardId")) && (string)r.Attribute("scope") != "Past").Remove();
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(game, OfflinePoolFixtures.Manifest(xml)); action(); }
        finally { property.SetValue(game, installed); }
    }
    internal static void AnswerAll(Game game)
    {
        while (game.CurrentPlayer.Choice != null) PlayerDecisionProof.Answer(game, game.CurrentPlayer.Choice.Choices[0]);
    }
    internal static void Resume(Game game, Action<Game> action) => PoolPilotProof.Resume(game, action);
}
