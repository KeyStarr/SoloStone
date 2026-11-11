using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class OfflinePoolSamplingProof
{
    internal static void Run()
    {
        OfflinePoolFixtures.Check(!typeof(OfflineCardPools).GetMethods().Any(m =>
            (m.Name == "Pick" || m.Name == "SampleDistinct") && m.GetParameters()[0].ParameterType == typeof(OfflinePoolManifest)),
            "public sampling requires a Game so legacy replay cannot bypass its guard");
        var filter = OfflinePoolFilter.Random();
        foreach (int size in new[] { 0, 1, 2, 3, 4, 6 })
        {
            var manifest = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture(size));
            var candidates = OfflineCardPools.Candidates(manifest, CardClass.MAGE, filter, 0);
            var random = new Util.DeepCloneableRandom(81);
            var expected = random.Clone();
            var before = random.CaptureState();
            if (size > 3) { expected.Next(size); expected.Next(size - 1); expected.Next(size - 2); }
            var offers = OfflineCardPools.SampleDistinct(manifest, random, filter, CardClass.MAGE, 0);
            OfflinePoolFixtures.Check(offers.Length == Math.Min(size, 3) && offers.Distinct().Count() == offers.Length, "distinct Discover size " + size);
            OfflinePoolFixtures.Check(random.CaptureState().Equals(expected.CaptureState()), "Discover exact RNG words/cursor " + size);
            if (size <= 3)
                OfflinePoolFixtures.Check(offers.SequenceEqual(candidates) && random.CaptureState().Equals(before), "small sorted pool, no RNG " + size);
            var pickExpected = random.Clone();
            var expectedPick = size == 0 ? null : candidates[pickExpected.Next(size)];
            var pick = OfflineCardPools.Pick(manifest, random, filter, CardClass.MAGE, 0);
            OfflinePoolFixtures.Check(pick == expectedPick && random.CaptureState().Equals(pickExpected.CaptureState()), "Pick exact bounded draw including singleton " + size);
            if (offers.Length > 0) offers[0] = null;
            OfflinePoolFixtures.Check(candidates.All(c => c != null), "temporary sample cannot mutate candidates");
        }

        var six = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture());
        var pool = OfflineCardPools.Candidates(six, CardClass.MAGE, filter, 0);
        // Seeded known answers are indices in ascending dbfId order, independent of fixture identities.
        // Independently calculated from the pinned RNG recurrence, including both state words.
        foreach (var vector in new[] {
            (Seed: 1L, Expected: new[] { 1, 5, 2 }, State0: -146043798543901056L, State1: 7638177738019497566L),
            (Seed: 81L, Expected: new[] { 4, 3, 1 }, State0: -5829768810491024973L, State1: -6983372910683228841L)
        })
        {
            var random = new Util.DeepCloneableRandom(vector.Seed);
            var offers = OfflineCardPools.SampleDistinct(six, random, filter, CardClass.MAGE, 0);
            var actual = offers.Select(c => pool.ToList().IndexOf(c)).ToArray();
            OfflinePoolFixtures.Check(actual.SequenceEqual(vector.Expected), "known vector seed " + vector.Seed + ": " + string.Join(",", actual));
            OfflinePoolFixtures.Check(random.CaptureState().Equals(new RngState(vector.State0, vector.State1, 3)), "known RNG state seed " + vector.Seed);
        }
        // Any-class sampling must remain independent of the controller class.
        var randomA = new Util.DeepCloneableRandom(1234);
        var randomB = randomA.Clone();
        for (int i = 0; i < 100; i++)
        {
            var mage = OfflineCardPools.SampleDistinct(six, randomA, filter, CardClass.MAGE, 0);
            var druid = OfflineCardPools.SampleDistinct(six, randomB, filter, CardClass.DRUID, 0);
            OfflinePoolFixtures.Check(mage.SequenceEqual(druid) && randomA.CaptureState().Equals(randomB.CaptureState()), "unweighted class-independent slots");
        }
        // Default Discover must also use one slot per identity, not legacy 4x class weighting.
        var mixedXml = OfflinePoolFixtures.Fixture();
        var mixedRows = mixedXml.Root.Element("Members").Elements("Card")
            .Where(c => (string)c.Attribute("scope") == "Standard").OrderBy(c => (int)c.Attribute("dbfId")).ToArray();
        for (int i = 0; i < mixedRows.Length; i++)
        {
            mixedRows[i].Elements("Class").Remove();
            mixedRows[i].Add(new XElement("Class", new XAttribute("name", i % 2 == 0 ? "NEUTRAL" : "MAGE")));
        }
        var mixed = OfflinePoolFixtures.Manifest(mixedXml);
        var discover = OfflinePoolFilter.Discover();
        var mixedIds = mixedRows.Select(c => (int)c.Attribute("dbfId")).ToArray();
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(mixed, CardClass.MAGE, discover, 0)
            .Select(c => c.AssetId).SequenceEqual(mixedIds), "default Discover six mixed neutral/controller candidates");
        foreach (var vector in new[] {
            (Seed: 1L, Expected: new[] { 1, 5, 2 }, State0: -146043798543901056L, State1: 7638177738019497566L),
            (Seed: 81L, Expected: new[] { 4, 3, 1 }, State0: -5829768810491024973L, State1: -6983372910683228841L)
        })
        {
            var random = new Util.DeepCloneableRandom(vector.Seed);
            var offers = OfflineCardPools.SampleDistinct(mixed, random, discover, CardClass.MAGE, 0);
            OfflinePoolFixtures.Check(offers.Select(c => c.AssetId).SequenceEqual(vector.Expected.Select(i => mixedIds[i])),
                "default Discover flat-pool known answer seed " + vector.Seed);
            OfflinePoolFixtures.Check(random.CaptureState().Equals(new RngState(vector.State0, vector.State1, 3)),
                "default Discover exact RNG state seed " + vector.Seed);
        }
        var singleton = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture(1));
        var repeated = new Util.DeepCloneableRandom(81);
        var first = OfflineCardPools.Pick(singleton, repeated, filter, CardClass.MAGE, 0);
        var second = OfflineCardPools.Pick(singleton, repeated, filter, CardClass.MAGE, 0);
        OfflinePoolFixtures.Check(first == second && repeated.DrawCount == 2, "independent picks with replacement");
        var control = new Util.DeepCloneableRandom(998);
        var actualPicks = control.Clone();
        for (int i = 0; i < 100; i++)
            OfflinePoolFixtures.Check(OfflineCardPools.Pick(six, actualPicks, filter, CardClass.MAGE, 0) == pool[control.Next(pool.Count)], "no cached sampled result");
        OfflinePoolFixtures.Check(actualPicks.CaptureState().Equals(control.CaptureState()), "repeated Pick full RNG state");
        Console.WriteLine("PASS offline pool sampling: 0/1/2/3/4/6 candidates; seeded vectors; exact RNG state; unweighted distinct offers; replacement; immutable cached candidates");
    }
}
