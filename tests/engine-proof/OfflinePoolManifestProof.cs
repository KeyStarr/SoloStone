using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Snapshots;

internal static class OfflinePoolManifestProof
{
    private static void Immutable(IList list, object value)
    {
        try { list[0] = value; }
        catch (NotSupportedException) { return; }
        throw new Exception("mutable offline pool collection");
    }

    // Exercise the real field codec before STEP 4 attaches the manifest to Game.
    // Reflection stays in tests; production needs no alternate snapshot root API.
    private static void SnapshotRoundTrip(OfflinePoolManifest manifest)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var codecType = typeof(SnapshotCodec);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        var saving = new SnapshotCodec();
        codecType.GetField("_writer", flags).SetValue(saving, writer);
        codecType.GetMethod("Write", flags).Invoke(saving, new object[] { manifest, 0 });
        writer.Flush();
        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var loading = new SnapshotCodec();
        codecType.GetField("_reader", flags).SetValue(loading, reader);
        var restored = (OfflinePoolManifest)codecType.GetMethod("Read", flags).Invoke(loading, new object[] { 0 });
        OfflinePoolFixtures.Check(stream.Position == stream.Length && !ReferenceEquals(manifest, restored), "manifest codec round-trip");
        OfflinePoolFixtures.Check(restored.Hash == manifest.Hash && restored.CatalogDigest == manifest.CatalogDigest &&
            restored.CanonicalText() == manifest.CanonicalText() &&
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(restored.CanonicalText()))).ToLowerInvariant() == restored.Hash,
            "snapshot preserves manifest contents and canonical hash");
        var before = manifest.Standard.Concat(manifest.Past).ToArray();
        var after = restored.Standard.Concat(restored.Past).ToArray();
        for (int i = 0; i < before.Length; i++)
        {
            OfflinePoolFixtures.Check(after[i].Tags.OrderBy(p => p.Key).SequenceEqual(before[i].Tags.OrderBy(p => p.Key)) &&
                after[i].Classes.SequenceEqual(before[i].Classes) && after[i].Races.SequenceEqual(before[i].Races),
                "snapshot preserves member filter metadata");
            try { ((IDictionary<int, int>)after[i].Tags)[(int)GameTag.TAUNT] = 99; throw new Exception("mutable restored tags"); }
            catch (NotSupportedException) { }
        }
        Immutable((IList)restored.Standard, null); Immutable((IList)restored.Past, null); Immutable((IList)restored.Sets, "OTHER");
    }

    internal static void Run()
    {
        // Independently encoded from the M0 catalog in Python; pins the canonical byte format.
        var installed = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.M0Catalog());
        OfflinePoolFixtures.Check(installed.CatalogDigest == "22931eaead9901179a1b7ddeba10e9a2f73f0332079218385e440a1ff4862354" &&
            installed.Hash == "f58e071fe154ec7c0fcd89eb74ab0ec4ef40e672840d0bbe89ea5c98819490de", "M0 canonical known-answer hash");
        SnapshotRoundTrip(installed);
        var xml = OfflinePoolFixtures.Fixture();
        var policy = OfflinePoolPolicy.Parse(xml);
        var manifest = policy.BuildManifest();
        SnapshotRoundTrip(manifest);
        var reverse = new XDocument(xml);
        var rows = reverse.Root.Element("Members").Elements("Card").Reverse().ToArray();
        reverse.Root.Element("Members").ReplaceNodes(rows);
        var reordered = OfflinePoolFixtures.Manifest(reverse);
        OfflinePoolFixtures.Check(manifest.Hash == reordered.Hash && manifest.CatalogDigest == reordered.CatalogDigest, "registration order independent");
        OfflinePoolFixtures.Check(manifest.Standard.Select(m => m.DbfId).SequenceEqual(manifest.Standard.Select(m => m.DbfId).OrderBy(n => n)) &&
            manifest.Past.Select(m => m.DbfId).SequenceEqual(manifest.Past.Select(m => m.DbfId).OrderBy(n => n)), "both scopes dbfId sorted");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            OfflinePoolFixtures.Check(policy.BuildManifest().Hash == manifest.Hash, "culture invariant hash");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
        var canonical = manifest.CanonicalText();
        OfflinePoolFixtures.Check(!canonical.Contains('\r') && canonical.EndsWith('\n') && !canonical.Contains(manifest.Hash) &&
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant() == manifest.Hash, "UTF-8 LF canonical hash excludes itself");
        OfflinePoolFixtures.Check(new OfflinePoolManifest(policy, "offline-standard/2", manifest.SamplingVersion).Hash != manifest.Hash &&
            new OfflinePoolManifest(policy, manifest.PolicyVersion, "partial-fisher-yates/2").Hash != manifest.Hash, "policy/sampling version hashed");
        void Changed(Action<XDocument> mutate, string reason)
        {
            var changed = new XDocument(xml); mutate(changed);
            var next = OfflinePoolFixtures.Manifest(changed);
            OfflinePoolFixtures.Check(next.Hash != manifest.Hash && next.CatalogDigest != manifest.CatalogDigest, "hash covers " + reason);
        }
        Changed(d => d.Root.Element("Members").Elements("Card").First().Remove(), "membership");
        foreach (var pair in new[] { ("cost", "9"), ("type", "HERO"), ("rarity", "LEGENDARY"), ("spellSchool", "FIRE") })
            Changed(d => d.Root.Element("Members").Elements("Card").First().SetAttributeValue(pair.Item1, pair.Item2), pair.Item1);
        Changed(d => d.Root.Element("Members").Elements("Card").First().Element("Class").SetAttributeValue("name", "WARLOCK"), "classes");
        Changed(d => d.Root.Element("Members").Elements("Card").First().Element("Race").SetAttributeValue("name", "ALL"), "races");
        Changed(d => d.Root.Element("Members").Elements("Card").First().Element("Tag").SetAttributeValue("value", 2), "keyword values");
        Changed(d => d.Root.Element("Source").SetAttributeValue("sha256", new string('a', 64)), "export provenance");

        Immutable((IList)manifest.Standard, null); Immutable((IList)manifest.Past, null); Immutable((IList)manifest.Sets, "OTHER");
        Immutable((IList)manifest.Standard[0].Classes, "OTHER"); Immutable((IList)manifest.Standard[0].Races, "OTHER");
        try { ((IDictionary<int, int>)manifest.Standard[0].Tags)[(int)GameTag.TAUNT] = 99; throw new Exception("mutable metadata tags"); }
        catch (NotSupportedException) { }
        xml.Root.Element("Members").RemoveNodes();
        OfflinePoolFixtures.Check(manifest.Standard.Count == 6 && policy.BuildManifest().Hash == manifest.Hash, "input XML detached");

        var filter = OfflinePoolFilter.Discover();
        var baseline = OfflineCardPools.Candidates(manifest, CardClass.MAGE, filter, 0);
        OfflinePoolFixtures.Check(ReferenceEquals(baseline, OfflineCardPools.Candidates(reordered, CardClass.MAGE, filter, 0)), "same identity shares immutable candidates");
        Immutable((IList)baseline, null);
        var otherHero = OfflineCardPools.Candidates(manifest, CardClass.DRUID, filter, 0);
        var otherFilter = OfflineCardPools.Candidates(manifest, CardClass.MAGE, OfflinePoolFilter.Random(), 0);
        var otherSource = OfflineCardPools.Candidates(manifest, CardClass.MAGE, filter, baseline[0].AssetId);
        var otherManifest = OfflineCardPools.Candidates(OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture(1)), CardClass.MAGE, filter, 0);
        OfflinePoolFixtures.Check(baseline.Count == 2 && otherHero.Count == 3 && otherFilter.Count == 6 && otherSource.Count == 1 && otherManifest.Count == 1,
            "cache isolation across class/filter/source/manifest");
        foreach (var result in new[] { otherHero, otherFilter, otherSource, otherManifest })
            OfflinePoolFixtures.Check(!ReferenceEquals(baseline, result), "separate cache key");
        // Same member IDs, changed metadata must also bypass the old candidate cache.
        var metadata = OfflinePoolFixtures.Fixture();
        metadata.Root.Element("Members").Elements("Card").First().Element("Class").SetAttributeValue("name", "WARLOCK");
        OfflinePoolFixtures.Check(OfflineCardPools.Candidates(OfflinePoolFixtures.Manifest(metadata), CardClass.MAGE, filter, 0).Count == 1, "metadata cache isolation");
        Console.WriteLine("PASS offline pool manifest: canonical SHA-256; order/culture independent; membership/metadata/provenance/versions hashed; immutable collections; snapshot round-trip/hash; isolated cache");
    }
}
